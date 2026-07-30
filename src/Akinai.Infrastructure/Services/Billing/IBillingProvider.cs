using Akinai.Shared.Billing;

namespace Akinai.Infrastructure.Services.Billing;

/// <summary>
/// The seam where a real payment provider plugs in.
///
/// Everything above this interface — plans, entitlements, subscription state,
/// the audit trail, the whole UI — is provider-agnostic and already final. To
/// go live, add a StripeBillingProvider that returns a real hosted-checkout URL
/// from <see cref="CreateCheckoutUrlAsync"/>, and call
/// ISubscriptionService.CompleteCheckoutAsync from a signature-verified webhook
/// instead of from the simulated payment page. Nothing else has to change.
/// </summary>
public interface IBillingProvider
{
    /// <summary>Shown on the billing page so it is obvious what is handling payment.</summary>
    string Name { get; }

    /// <summary>True when no money moves. Drives the warnings in the UI.</summary>
    bool IsSimulated { get; }

    /// <summary>
    /// True when the provider renews the subscription on its own.
    ///
    /// Deliberately separate from <see cref="IsSimulated"/>. A provider in test
    /// mode takes no real money but still renews by itself, so conflating the
    /// two put an "Extend by another month" button on a self-renewing plan —
    /// which would have started a second subscription and billed twice.
    /// </summary>
    bool RenewsAutomatically { get; }

    /// <summary>
    /// Where to send the browser to pay. The session has already been created
    /// and priced; the provider only decides where the user goes next.
    /// </summary>
    Task<string> CreateCheckoutUrlAsync(
        Guid sessionId, BillingPlan plan, BillingCycle cycle, decimal amount, string currency,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Stand-in provider that takes no money.
///
/// It sends the browser to an internal page that looks like a checkout and
/// grants the plan on click. Every subscription it creates is flagged
/// <c>Simulated</c> in the billing history, so test subscriptions can never be
/// mistaken for revenue.
/// </summary>
public class SimulatedBillingProvider : IBillingProvider
{
    public string Name => "Simulated";

    public bool IsSimulated => true;

    /// <summary>Periods are bought one at a time here; nothing renews on its own.</summary>
    public bool RenewsAutomatically => false;

    public Task<string> CreateCheckoutUrlAsync(
        Guid sessionId, BillingPlan plan, BillingCycle cycle, decimal amount, string currency,
        CancellationToken cancellationToken = default)
        => Task.FromResult($"billing/checkout/{sessionId}");
}
