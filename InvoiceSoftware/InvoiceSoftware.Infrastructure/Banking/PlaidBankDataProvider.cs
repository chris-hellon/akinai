using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using InvoiceSoftware.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvoiceSoftware.Infrastructure.Banking;

/// <summary>
/// Plaid implementation using Hosted Link (redirect flow): create a link_token with a
/// completion redirect → user completes in Plaid's hosted UI → we fetch the public_token via
/// link_token, exchange it for a long-lived access_token, then pull transactions/balances.
/// NOTE: Plaid amounts are positive for money OUT — we flip the sign to our convention.
/// Endpoints follow the current Plaid API; verify against docs before going live.
/// </summary>
public class PlaidBankDataProvider(
    HttpClient httpClient,
    ILogger<PlaidBankDataProvider> logger,
    IOptions<PlaidOptions> options) : IBankDataProvider
{
    private readonly PlaidOptions _options = options.Value;

    public string Name => "Plaid";
    public bool IsConfigured => _options.IsConfigured;
    public IReadOnlyCollection<string> SupportedCountries { get; } = BankingCountries.Plaid;
    public int Priority => 100;
    public string RedirectUri => _options.RedirectUri;

    // Plaid shows its own bank picker in Hosted Link.
    public Task<IReadOnlyList<ProviderInstitution>> GetInstitutionsAsync(string country, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<ProviderInstitution>>(
            _options.IsConfigured
                ? new List<ProviderInstitution> { new("plaid", "Choose your bank (via Plaid)", null) }
                : new List<ProviderInstitution>());

    public async Task<BankLinkSession> CreateLinkAsync(string institutionId, string country, string reference, string redirectUri, CancellationToken cancellationToken)
    {
        var body = new
        {
            client_id = _options.ClientId,
            secret = _options.Secret,
            user = new { client_user_id = reference },
            client_name = "Akinai",
            products = new[] { "transactions" },
            country_codes = new[] { (country ?? "us").ToUpperInvariant() },
            language = "en",
            hosted_link = new { completion_redirect_uri = $"{redirectUri}?ref={Uri.EscapeDataString(reference)}" }
        };
        var resp = await PostAsync<LinkTokenResponse>("/link/token/create", body, cancellationToken);
        if (resp is null || string.IsNullOrEmpty(resp.HostedLinkUrl))
            return new BankLinkSession(string.Empty, string.Empty, reference);
        return new BankLinkSession(resp.LinkToken ?? "", resp.HostedLinkUrl, reference);
    }

    public async Task<LinkResult> CompleteLinkAsync(BankConnection connection, IReadOnlyDictionary<string, string> callbackQuery, CancellationToken cancellationToken)
    {
        // Retrieve the public token created during the hosted session.
        var sessions = await PostAsync<LinkTokenGetResponse>("/link/token/get",
            new { client_id = _options.ClientId, secret = _options.Secret, link_token = connection.RequisitionId }, cancellationToken);

        var publicToken = sessions?.LinkSessions?
            .SelectMany(s => s.Results?.ItemAddResults ?? new())
            .Select(r => r.PublicToken)
            .FirstOrDefault(t => !string.IsNullOrEmpty(t));
        if (string.IsNullOrEmpty(publicToken))
            return new LinkResult(false, null, null);

        var exchange = await PostAsync<ExchangeResponse>("/item/public_token/exchange",
            new { client_id = _options.ClientId, secret = _options.Secret, public_token = publicToken }, cancellationToken);
        if (exchange is null || string.IsNullOrEmpty(exchange.AccessToken))
            return new LinkResult(false, null, null);

        var accounts = await PostAsync<AccountsResponse>("/accounts/get",
            new { client_id = _options.ClientId, secret = _options.Secret, access_token = exchange.AccessToken }, cancellationToken);
        var accountId = accounts?.Accounts?.FirstOrDefault()?.AccountId;
        if (accountId is null)
            return new LinkResult(false, null, null);

        // Plaid access tokens are long-lived (no refresh/expiry).
        return new LinkResult(true, accountId, null, AccessToken: exchange.AccessToken);
    }

    public async Task<IReadOnlyList<ProviderTransaction>> GetTransactionsAsync(BankConnection connection, DateOnly? from, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(connection.AccessToken)) return Array.Empty<ProviderTransaction>();
        var fromDate = from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-90));
        var to = DateOnly.FromDateTime(DateTime.UtcNow);

        var resp = await PostAsync<TransactionsResponse>("/transactions/get", new
        {
            client_id = _options.ClientId,
            secret = _options.Secret,
            access_token = connection.AccessToken,
            start_date = fromDate.ToString("yyyy-MM-dd"),
            end_date = to.ToString("yyyy-MM-dd"),
            options = new { count = 500, offset = 0 }
        }, cancellationToken);

        var results = new List<ProviderTransaction>();
        foreach (var t in resp?.Transactions ?? new())
        {
            if (string.IsNullOrWhiteSpace(t.TransactionId)) continue;
            if (!DateOnly.TryParse(t.Date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) continue;
            // Plaid: positive = money out. Our convention: positive = money in.
            results.Add(new ProviderTransaction(
                t.TransactionId,
                date,
                -t.Amount,
                t.IsoCurrencyCode ?? "",
                string.IsNullOrWhiteSpace(t.MerchantName) ? (t.Name ?? "(no description)") : t.MerchantName!,
                null));
        }
        return results;
    }

    public async Task<ProviderBalance?> GetBalanceAsync(BankConnection connection, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(connection.AccessToken)) return null;
        var resp = await PostAsync<AccountsResponse>("/accounts/balance/get",
            new { client_id = _options.ClientId, secret = _options.Secret, access_token = connection.AccessToken }, cancellationToken);
        var account = resp?.Accounts?.FirstOrDefault(a => a.AccountId == connection.ExternalAccountId)
                      ?? resp?.Accounts?.FirstOrDefault();
        var bal = account?.Balances;
        if (bal is null) return null;
        var amount = bal.Available ?? bal.Current;
        if (amount is null) return null;
        return new ProviderBalance(amount.Value, bal.IsoCurrencyCode ?? "");
    }

    private async Task<T?> PostAsync<T>(string path, object body, CancellationToken cancellationToken)
    {
        var resp = await httpClient.PostAsJsonAsync($"{_options.BaseUrl}{path}", body, cancellationToken);
        if (!resp.IsSuccessStatusCode)
        {
            logger.LogWarning("Plaid {Path} failed ({Status})", path, (int)resp.StatusCode);
            return default;
        }
        return await resp.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
    }

    // ---- response DTOs ----
    private record LinkTokenResponse(
        [property: JsonPropertyName("link_token")] string? LinkToken,
        [property: JsonPropertyName("hosted_link_url")] string? HostedLinkUrl);

    private record LinkTokenGetResponse([property: JsonPropertyName("link_sessions")] List<LinkSession>? LinkSessions);
    private record LinkSession([property: JsonPropertyName("results")] LinkSessionResults? Results);
    private record LinkSessionResults([property: JsonPropertyName("item_add_results")] List<ItemAddResult>? ItemAddResults);
    private record ItemAddResult([property: JsonPropertyName("public_token")] string? PublicToken);

    private record ExchangeResponse([property: JsonPropertyName("access_token")] string? AccessToken);

    private record AccountsResponse([property: JsonPropertyName("accounts")] List<PlaidAccount>? Accounts);
    private record PlaidAccount(
        [property: JsonPropertyName("account_id")] string AccountId,
        [property: JsonPropertyName("balances")] PlaidBalances? Balances);
    private record PlaidBalances(
        [property: JsonPropertyName("available")] decimal? Available,
        [property: JsonPropertyName("current")] decimal? Current,
        [property: JsonPropertyName("iso_currency_code")] string? IsoCurrencyCode);

    private record TransactionsResponse([property: JsonPropertyName("transactions")] List<PlaidTransaction>? Transactions);
    private record PlaidTransaction(
        [property: JsonPropertyName("transaction_id")] string? TransactionId,
        [property: JsonPropertyName("date")] string? Date,
        [property: JsonPropertyName("amount")] decimal Amount,
        [property: JsonPropertyName("iso_currency_code")] string? IsoCurrencyCode,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("merchant_name")] string? MerchantName);
}
