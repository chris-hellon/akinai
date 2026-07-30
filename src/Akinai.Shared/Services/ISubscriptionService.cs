using Akinai.Shared.Api;
using Akinai.Shared.Billing;
using Akinai.Shared.Dtos.Billing;

namespace Akinai.Shared.Services;

/// <summary>
/// Every state transition a subscription can undergo, in one place.
///
/// Nothing else writes the subscription columns on the user. That is the point:
/// each transition here also appends to the billing history, so the audit trail
/// cannot fall out of step with the current state.
/// </summary>
public interface ISubscriptionService
{
    Task<SubscriptionDto> GetAsync(string userId, CancellationToken cancellationToken = default);

    Task<PaginatedResponse<SubscriptionEventDto>> GetHistoryAsync(
        string userId, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a checkout for a paid plan. The price is fixed server-side at this
    /// point and recorded on the session, so completing it cannot be talked
    /// into a different amount.
    /// </summary>
    Task<CheckoutSessionDto> StartCheckoutAsync(
        string userId, BillingPlan plan, BillingCycle cycle, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a checkout paid and grants the plan.
    ///
    /// While billing is simulated this is called by the fake payment page. With
    /// a real provider it is called by the verified webhook instead — which is
    /// why it takes a session id and reads everything else from the stored
    /// session rather than from whoever is asking.
    /// </summary>
    Task<CheckoutResultDto> CompleteCheckoutAsync(
        string userId, Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>Chooses the free plan, or records that free was chosen at signup.</summary>
    Task<SubscriptionDto> SelectFreeAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Cancels at period end. Entitlements survive until then.</summary>
    Task<SubscriptionDto> CancelAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Undoes a pending cancellation, while the period is still running.</summary>
    Task<SubscriptionDto> ResumeAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a plan directly, without payment — for selling by hand and for
    /// support. Always recorded against the admin who did it.
    /// </summary>
    Task<SubscriptionDto> AdminSetPlanAsync(
        string userId, BillingPlan plan, BillingCycle? cycle, int? months,
        string performedByEmail, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records the provider's own subscription id against the account, so later
    /// events about that subscription can be matched back to a user.
    /// </summary>
    Task LinkProviderSubscriptionAsync(
        string userId, string providerSubscriptionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// A recurring payment succeeded. Extends the paid period.
    ///
    /// Without this the account is charged every month and still lapses to Free
    /// the day after the first period ends — billed for something it no longer
    /// has. The provider is the source of truth for when the next period ends,
    /// so the date comes from them rather than being computed here.
    /// </summary>
    Task RecordRenewalAsync(
        string providerSubscriptionId, DateTime periodEndUtc, decimal amount, string currency,
        CancellationToken cancellationToken = default);

    /// <summary>A recurring payment failed. Entitlements resolve to free while past due.</summary>
    Task MarkPastDueAsync(string providerSubscriptionId, CancellationToken cancellationToken = default);

    /// <summary>The provider ended the subscription — cancelled, or unpaid too long.</summary>
    Task EndSubscriptionAsync(string providerSubscriptionId, CancellationToken cancellationToken = default);
}
