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

    Task<IReadOnlyList<SubscriptionEventDto>> GetHistoryAsync(string userId, CancellationToken cancellationToken = default);

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
}
