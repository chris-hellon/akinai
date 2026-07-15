using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using InvoiceSoftware.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvoiceSoftware.Infrastructure.Banking;

/// <summary>
/// TrueLayer Data API implementation. OAuth2 authorization-code flow: build an auth link
/// (state = our reference) → user authenticates and returns with a "code" → exchange for
/// access/refresh tokens → pull accounts/transactions/balances. Tokens live on the connection.
/// Endpoints follow the Data API v1; verify against current docs before going live.
/// </summary>
public class TrueLayerBankDataProvider(
    HttpClient httpClient,
    ILogger<TrueLayerBankDataProvider> logger,
    IOptions<TrueLayerOptions> options) : IBankDataProvider
{
    private readonly TrueLayerOptions _options = options.Value;

    public string Name => "TrueLayer";
    public bool IsConfigured => _options.IsConfigured;
    public IReadOnlyCollection<string> SupportedCountries { get; } = BankingCountries.UkEu;
    public int Priority => 90;
    public string RedirectUri => _options.RedirectUri;

    // TrueLayer shows its own bank picker in the auth dialog, so we surface a single entry
    // and let the user choose their bank there.
    public Task<IReadOnlyList<ProviderInstitution>> GetInstitutionsAsync(string country, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<ProviderInstitution>>(
            _options.IsConfigured
                ? new List<ProviderInstitution> { new("truelayer", "Choose your bank (via TrueLayer)", null) }
                : new List<ProviderInstitution>());

    public Task<BankLinkSession> CreateLinkAsync(string institutionId, string country, string reference, string redirectUri, CancellationToken cancellationToken)
    {
        var scope = "info accounts balance transactions offline_access";
        var url = $"{_options.AuthBase}/?response_type=code" +
                  $"&client_id={Uri.EscapeDataString(_options.ClientId)}" +
                  $"&scope={Uri.EscapeDataString(scope)}" +
                  $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                  $"&providers={Uri.EscapeDataString(_options.Providers)}" +
                  $"&state={Uri.EscapeDataString(reference)}";
        // No requisition concept in TrueLayer — the code comes back on the redirect.
        return Task.FromResult(new BankLinkSession(string.Empty, url, reference));
    }

    public async Task<LinkResult> CompleteLinkAsync(BankConnection connection, IReadOnlyDictionary<string, string> callbackQuery, CancellationToken cancellationToken)
    {
        if (!callbackQuery.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
            return new LinkResult(false, null, null);

        var token = await ExchangeCodeAsync(code, cancellationToken);
        if (token is null) return new LinkResult(false, null, null);

        var accountId = await GetFirstAccountIdAsync(token.AccessToken, cancellationToken);
        if (accountId is null) return new LinkResult(false, null, null);

        var expiresAt = DateTime.UtcNow.AddSeconds(token.ExpiresIn);
        return new LinkResult(
            Linked: true,
            AccountId: accountId,
            ConsentExpiresAt: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(90),
            AccessToken: token.AccessToken,
            RefreshToken: token.RefreshToken,
            TokenExpiresAt: expiresAt);
    }

    public async Task<IReadOnlyList<ProviderTransaction>> GetTransactionsAsync(BankConnection connection, DateOnly? from, CancellationToken cancellationToken)
    {
        var accessToken = await EnsureTokenAsync(connection, cancellationToken);
        if (accessToken is null || connection.ExternalAccountId is null) return Array.Empty<ProviderTransaction>();

        var fromDate = from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-90));
        var to = DateOnly.FromDateTime(DateTime.UtcNow);
        var url = $"{_options.ApiBase}/data/v1/accounts/{connection.ExternalAccountId}/transactions?from={fromDate:yyyy-MM-dd}T00:00:00Z&to={to:yyyy-MM-dd}T23:59:59Z";

        var resp = await SendAsync<TransactionsResponse>(url, accessToken, cancellationToken);
        var results = new List<ProviderTransaction>();
        foreach (var t in resp?.Results ?? new())
        {
            if (string.IsNullOrWhiteSpace(t.TransactionId)) continue;
            if (!DateTime.TryParse(t.Timestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var ts)) continue;
            results.Add(new ProviderTransaction(
                t.TransactionId,
                DateOnly.FromDateTime(ts),
                t.Amount,
                t.Currency ?? "",
                string.IsNullOrWhiteSpace(t.Description) ? (t.MerchantName ?? "(no description)") : t.Description,
                null));
        }
        return results;
    }

    public async Task<ProviderBalance?> GetBalanceAsync(BankConnection connection, CancellationToken cancellationToken)
    {
        var accessToken = await EnsureTokenAsync(connection, cancellationToken);
        if (accessToken is null || connection.ExternalAccountId is null) return null;

        var resp = await SendAsync<BalanceResponse>(
            $"{_options.ApiBase}/data/v1/accounts/{connection.ExternalAccountId}/balance", accessToken, cancellationToken);
        var b = resp?.Results?.FirstOrDefault();
        if (b is null) return null;
        return new ProviderBalance(b.Available ?? b.Current, b.Currency ?? "");
    }

    // ---- OAuth helpers ----
    private async Task<TokenResponse?> ExchangeCodeAsync(string code, CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["redirect_uri"] = _options.RedirectUri,
            ["code"] = code
        };
        return await PostTokenAsync(form, "exchange code", cancellationToken);
    }

    /// <summary>Return a valid access token, refreshing (and updating the connection) if expired.</summary>
    private async Task<string?> EnsureTokenAsync(BankConnection connection, CancellationToken cancellationToken)
    {
        if (connection.AccessToken is not null && connection.TokenExpiresAt is { } exp && DateTime.UtcNow < exp.AddSeconds(-60))
            return connection.AccessToken;

        if (string.IsNullOrEmpty(connection.RefreshToken))
            return connection.AccessToken; // nothing to refresh with

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["refresh_token"] = connection.RefreshToken
        };
        var token = await PostTokenAsync(form, "refresh token", cancellationToken);
        if (token is null) return connection.AccessToken;

        connection.SetTokens(
            token.AccessToken,
            string.IsNullOrEmpty(token.RefreshToken) ? connection.RefreshToken : token.RefreshToken,
            DateTime.UtcNow.AddSeconds(token.ExpiresIn));
        return connection.AccessToken;
    }

    private async Task<TokenResponse?> PostTokenAsync(Dictionary<string, string> form, string action, CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(form);
        var resp = await httpClient.PostAsync($"{_options.AuthBase}/connect/token", content, cancellationToken);
        if (!resp.IsSuccessStatusCode)
        {
            logger.LogWarning("TrueLayer {Action} failed ({Status})", action, (int)resp.StatusCode);
            return null;
        }
        return await resp.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: cancellationToken);
    }

    private async Task<string?> GetFirstAccountIdAsync(string accessToken, CancellationToken cancellationToken)
    {
        var resp = await SendAsync<AccountsResponse>($"{_options.ApiBase}/data/v1/accounts", accessToken, cancellationToken);
        return resp?.Results?.FirstOrDefault()?.AccountId;
    }

    private async Task<T?> SendAsync<T>(string url, string accessToken, CancellationToken cancellationToken)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new("Bearer", accessToken);
        var resp = await httpClient.SendAsync(req, cancellationToken);
        if (!resp.IsSuccessStatusCode)
        {
            logger.LogWarning("TrueLayer GET {Url} failed ({Status})", url, (int)resp.StatusCode);
            return default;
        }
        return await resp.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
    }

    // ---- response DTOs ----
    private record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);

    private record AccountsResponse([property: JsonPropertyName("results")] List<AccountResponse>? Results);
    private record AccountResponse([property: JsonPropertyName("account_id")] string AccountId);

    private record TransactionsResponse([property: JsonPropertyName("results")] List<TransactionResponse>? Results);
    private record TransactionResponse(
        [property: JsonPropertyName("transaction_id")] string? TransactionId,
        [property: JsonPropertyName("timestamp")] string? Timestamp,
        [property: JsonPropertyName("amount")] decimal Amount,
        [property: JsonPropertyName("currency")] string? Currency,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("merchant_name")] string? MerchantName);

    private record BalanceResponse([property: JsonPropertyName("results")] List<BalanceItem>? Results);
    private record BalanceItem(
        [property: JsonPropertyName("current")] decimal Current,
        [property: JsonPropertyName("available")] decimal? Available,
        [property: JsonPropertyName("currency")] string? Currency);
}
