using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using InvoiceSoftware.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvoiceSoftware.Infrastructure.Banking;

/// <summary>
/// GoCardless Bank Account Data (formerly Nordigen) implementation. Flow: token → create
/// requisition (link) → user authenticates → resolve account → pull transactions/balances.
/// Endpoints follow the v2 API; verify against current docs before going live.
/// </summary>
public class GoCardlessBankDataProvider(
    HttpClient httpClient,
    ILogger<GoCardlessBankDataProvider> logger,
    IOptions<GoCardlessOptions> options) : IBankDataProvider
{
    private readonly GoCardlessOptions _options = options.Value;

    private static string? _accessToken;
    private static DateTime _tokenExpiry = DateTime.MinValue;
    private static readonly SemaphoreSlim _tokenLock = new(1, 1);

    public string Name => "GoCardless";
    public bool IsConfigured => _options.IsConfigured;
    public IReadOnlyCollection<string> SupportedCountries { get; } = BankingCountries.UkEu;
    public int Priority => 80;
    public string RedirectUri => _options.RedirectUri;

    public async Task<IReadOnlyList<ProviderInstitution>> GetInstitutionsAsync(string country, CancellationToken cancellationToken)
    {
        await AuthorizeAsync(cancellationToken);
        var list = await httpClient.GetFromJsonAsync<List<InstitutionResponse>>(
            $"/api/v2/institutions/?country={Uri.EscapeDataString(country)}", cancellationToken);
        return (list ?? new())
            .Select(i => new ProviderInstitution(i.Id, i.Name, i.Logo))
            .ToList();
    }

    public async Task<BankLinkSession> CreateLinkAsync(string institutionId, string country, string reference, string redirectUri, CancellationToken cancellationToken)
    {
        await AuthorizeAsync(cancellationToken);
        var body = new
        {
            redirect = redirectUri,
            institution_id = institutionId,
            reference,
            user_language = "EN"
        };
        var resp = await httpClient.PostAsJsonAsync("/api/v2/requisitions/", body, cancellationToken);
        await EnsureSuccess(resp, "create requisition");
        var req = await resp.Content.ReadFromJsonAsync<RequisitionResponse>(cancellationToken: cancellationToken)
                  ?? throw new InvalidOperationException("Empty requisition response.");
        return new BankLinkSession(req.Id, req.Link ?? "", reference);
    }

    public async Task<LinkResult> CompleteLinkAsync(BankConnection connection, IReadOnlyDictionary<string, string> callbackQuery, CancellationToken cancellationToken)
    {
        await AuthorizeAsync(cancellationToken);
        var req = await httpClient.GetFromJsonAsync<RequisitionResponse>($"/api/v2/requisitions/{connection.RequisitionId}/", cancellationToken);
        var accountId = req?.Accounts?.FirstOrDefault();
        var linked = string.Equals(req?.Status, "LN", StringComparison.OrdinalIgnoreCase) && accountId is not null;
        return new LinkResult(linked, accountId, null);
    }

    public async Task<IReadOnlyList<ProviderTransaction>> GetTransactionsAsync(BankConnection connection, DateOnly? from, CancellationToken cancellationToken)
    {
        var accountId = connection.ExternalAccountId;
        if (accountId is null) return Array.Empty<ProviderTransaction>();
        await AuthorizeAsync(cancellationToken);
        var url = $"/api/v2/accounts/{accountId}/transactions/";
        if (from is { } f) url += $"?date_from={f:yyyy-MM-dd}";
        var resp = await httpClient.GetFromJsonAsync<TransactionsResponse>(url, cancellationToken);

        var results = new List<ProviderTransaction>();
        foreach (var t in resp?.Transactions?.Booked ?? new())
        {
            var externalId = t.TransactionId ?? t.InternalTransactionId;
            if (string.IsNullOrWhiteSpace(externalId)) continue; // no stable id to dedup on
            if (!decimal.TryParse(t.TransactionAmount?.Amount, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount)) continue;
            if (!DateOnly.TryParse(t.BookingDate ?? t.ValueDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) continue;

            var description = FirstNonEmpty(t.RemittanceInformationUnstructured, t.CreditorName, t.DebtorName) ?? "(no description)";
            results.Add(new ProviderTransaction(externalId, date, amount, t.TransactionAmount!.Currency ?? "", description, null));
        }
        return results;
    }

    public async Task<ProviderBalance?> GetBalanceAsync(BankConnection connection, CancellationToken cancellationToken)
    {
        var accountId = connection.ExternalAccountId;
        if (accountId is null) return null;
        await AuthorizeAsync(cancellationToken);
        var resp = await httpClient.GetFromJsonAsync<BalancesResponse>($"/api/v2/accounts/{accountId}/balances/", cancellationToken);
        var balances = resp?.Balances ?? new();
        var chosen = balances.FirstOrDefault(b => b.BalanceType is "interimAvailable" or "closingBooked" or "expected")
                     ?? balances.FirstOrDefault();
        if (chosen?.BalanceAmount is null) return null;
        if (!decimal.TryParse(chosen.BalanceAmount.Amount, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount)) return null;
        return new ProviderBalance(amount, chosen.BalanceAmount.Currency ?? "");
    }

    // ---- auth ----
    private async Task AuthorizeAsync(CancellationToken cancellationToken)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("GoCardless is not configured (missing SecretId/SecretKey).");

        if (_accessToken is not null && DateTime.UtcNow < _tokenExpiry)
        {
            httpClient.DefaultRequestHeaders.Authorization = new("Bearer", _accessToken);
            return;
        }

        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (_accessToken is null || DateTime.UtcNow >= _tokenExpiry)
            {
                var resp = await httpClient.PostAsJsonAsync("/api/v2/token/new/",
                    new { secret_id = _options.SecretId, secret_key = _options.SecretKey }, cancellationToken);
                await EnsureSuccess(resp, "obtain token");
                var token = await resp.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: cancellationToken)
                            ?? throw new InvalidOperationException("Empty token response.");
                _accessToken = token.Access;
                _tokenExpiry = DateTime.UtcNow.AddSeconds(Math.Max(60, token.AccessExpires - 60));
            }
            httpClient.DefaultRequestHeaders.Authorization = new("Bearer", _accessToken);
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private async Task EnsureSuccess(HttpResponseMessage resp, string action)
    {
        if (resp.IsSuccessStatusCode) return;
        var body = await resp.Content.ReadAsStringAsync();
        logger.LogWarning("GoCardless {Action} failed ({Status}): {Body}", action, (int)resp.StatusCode, body);
        throw new InvalidOperationException($"GoCardless {action} failed ({(int)resp.StatusCode}).");
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    // ---- response DTOs ----
    private record TokenResponse(
        [property: JsonPropertyName("access")] string Access,
        [property: JsonPropertyName("access_expires")] int AccessExpires);

    private record InstitutionResponse(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("logo")] string? Logo);

    private record RequisitionResponse(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("link")] string? Link,
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("accounts")] List<string>? Accounts);

    private record TransactionsResponse(
        [property: JsonPropertyName("transactions")] TransactionBuckets? Transactions);

    private record TransactionBuckets(
        [property: JsonPropertyName("booked")] List<TransactionResponse>? Booked,
        [property: JsonPropertyName("pending")] List<TransactionResponse>? Pending);

    private record TransactionResponse(
        [property: JsonPropertyName("transactionId")] string? TransactionId,
        [property: JsonPropertyName("internalTransactionId")] string? InternalTransactionId,
        [property: JsonPropertyName("bookingDate")] string? BookingDate,
        [property: JsonPropertyName("valueDate")] string? ValueDate,
        [property: JsonPropertyName("transactionAmount")] AmountResponse? TransactionAmount,
        [property: JsonPropertyName("remittanceInformationUnstructured")] string? RemittanceInformationUnstructured,
        [property: JsonPropertyName("creditorName")] string? CreditorName,
        [property: JsonPropertyName("debtorName")] string? DebtorName);

    private record AmountResponse(
        [property: JsonPropertyName("amount")] string? Amount,
        [property: JsonPropertyName("currency")] string? Currency);

    private record BalancesResponse(
        [property: JsonPropertyName("balances")] List<BalanceResponse>? Balances);

    private record BalanceResponse(
        [property: JsonPropertyName("balanceAmount")] AmountResponse? BalanceAmount,
        [property: JsonPropertyName("balanceType")] string? BalanceType);
}
