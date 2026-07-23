using Akinai.Shared.Dtos.Billing;

namespace Akinai.Shared.Services;

/// <summary>
/// Resolves what an account may currently do.
///
/// Every paid feature is gated through this, server-side. The UI hides things
/// it knows are unavailable, but that is a courtesy — hiding a button has never
/// stopped a crafted request, so the handler is where the decision is actually
/// made.
/// </summary>
public interface IEntitlementService
{
    /// <summary>
    /// The account's effective entitlements, which are not simply its stored
    /// plan: a lapsed, past-due or expired subscription resolves to free.
    /// </summary>
    Task<EntitlementsDto> GetAsync(string userId, CancellationToken cancellationToken = default);
}
