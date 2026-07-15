using InvoiceSoftware.Domain.Common;
using InvoiceSoftware.Domain.Enums;

namespace InvoiceSoftware.Domain.Entities;

/// <summary>
/// A live open-banking connection to a user's bank account (via an aggregator such as
/// GoCardless Bank Account Data). Transactions are pulled from here into BankTransactions.
/// </summary>
public class BankConnection : AggregateRoot, IAuditableEntity
{
    public string UserId { get; private set; } = null!;

    /// <summary>Provider key, e.g. "GoCardless".</summary>
    public string Provider { get; private set; } = null!;

    public string InstitutionId { get; private set; } = null!;
    public string InstitutionName { get; private set; } = null!;

    /// <summary>Our correlation reference, echoed back on the provider's redirect.</summary>
    public string Reference { get; private set; } = null!;

    /// <summary>The provider's link/requisition id used to fetch linked accounts.</summary>
    public string RequisitionId { get; private set; } = null!;

    /// <summary>The provider's account id, populated once the user has authenticated.</summary>
    public string? ExternalAccountId { get; private set; }

    public BankConnectionStatus Status { get; private set; } = BankConnectionStatus.Pending;
    public DateOnly? ConsentExpiresAt { get; private set; }
    public DateTime? LastSyncedAt { get; private set; }
    public string? LastError { get; private set; }

    // OAuth tokens for providers that use per-connection auth (TrueLayer, Plaid).
    // SECURITY: these are the end user's bank access tokens — encrypt at rest in production
    // (e.g. ASP.NET Data Protection or column encryption). Stored plain here for the scaffold.
    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTime? TokenExpiresAt { get; private set; }

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    private BankConnection() { }

    public static BankConnection Create(
        string userId, string provider, string institutionId, string institutionName,
        string reference, string requisitionId)
        => new()
        {
            UserId = userId,
            Provider = provider,
            InstitutionId = institutionId,
            InstitutionName = institutionName,
            Reference = reference,
            RequisitionId = requisitionId,
            Status = BankConnectionStatus.Pending
        };

    public void MarkLinked(string accountId, DateOnly? consentExpires)
    {
        ExternalAccountId = accountId;
        ConsentExpiresAt = consentExpires;
        Status = BankConnectionStatus.Linked;
        LastError = null;
    }

    public void MarkSynced(DateTime at)
    {
        LastSyncedAt = at;
        if (Status == BankConnectionStatus.Error) Status = BankConnectionStatus.Linked;
    }

    public void MarkError(string? error)
    {
        Status = BankConnectionStatus.Error;
        LastError = error;
    }

    public void SetTokens(string? accessToken, string? refreshToken, DateTime? expiresAt)
    {
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        TokenExpiresAt = expiresAt;
    }

    public void MarkExpired() => Status = BankConnectionStatus.Expired;
}
