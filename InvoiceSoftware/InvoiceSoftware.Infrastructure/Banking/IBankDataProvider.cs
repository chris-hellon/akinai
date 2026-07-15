using InvoiceSoftware.Domain.Entities;

namespace InvoiceSoftware.Infrastructure.Banking;

/// <summary>
/// Abstraction over an open-banking data aggregator (TrueLayer, GoCardless, Plaid, …) or a
/// direct bank API. Supports both shared-key providers (GoCardless) and per-connection OAuth
/// providers (TrueLayer/Plaid) — the connection carries any tokens the provider needs.
/// </summary>
public interface IBankDataProvider
{
    /// <summary>Provider key stored on the connection, e.g. "TrueLayer".</summary>
    string Name { get; }

    /// <summary>Whether credentials are configured; skip provider calls when false.</summary>
    bool IsConfigured { get; }

    /// <summary>ISO 3166 country codes (lowercase) this provider can connect banks in.</summary>
    IReadOnlyCollection<string> SupportedCountries { get; }

    /// <summary>Higher wins when several providers cover the same country.</summary>
    int Priority { get; }

    /// <summary>Configured redirect URI the bank returns the user to.</summary>
    string RedirectUri { get; }

    Task<IReadOnlyList<ProviderInstitution>> GetInstitutionsAsync(string country, CancellationToken cancellationToken);

    /// <summary>Create a bank-authentication link. The user is redirected to <c>AuthUrl</c>.</summary>
    Task<BankLinkSession> CreateLinkAsync(string institutionId, string country, string reference, string redirectUri, CancellationToken cancellationToken);

    /// <summary>
    /// Complete the link after the redirect. <paramref name="callbackQuery"/> holds the redirect
    /// query params (e.g. "code" for OAuth). Any tokens are returned on the result for storage.
    /// </summary>
    Task<LinkResult> CompleteLinkAsync(BankConnection connection, IReadOnlyDictionary<string, string> callbackQuery, CancellationToken cancellationToken);

    /// <summary>Pull transactions. The connection carries the account id and any OAuth tokens.</summary>
    Task<IReadOnlyList<ProviderTransaction>> GetTransactionsAsync(BankConnection connection, DateOnly? from, CancellationToken cancellationToken);

    Task<ProviderBalance?> GetBalanceAsync(BankConnection connection, CancellationToken cancellationToken);
}

public record ProviderInstitution(string Id, string Name, string? LogoUrl);

public record BankLinkSession(string RequisitionId, string AuthUrl, string Reference);

public record LinkResult(
    bool Linked,
    string? AccountId,
    DateOnly? ConsentExpiresAt,
    string? AccessToken = null,
    string? RefreshToken = null,
    DateTime? TokenExpiresAt = null);

public record ProviderTransaction(
    string ExternalId,
    DateOnly Date,
    decimal Amount,          // signed: positive in, negative out
    string Currency,
    string Description,
    string? Reference);

public record ProviderBalance(decimal Amount, string Currency);
