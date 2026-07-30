using BlazorUtils.EasyApi;
using Akinai.Shared.Billing;
using Akinai.Shared.Dtos.Billing;

namespace Akinai.Shared.Api.Billing;

/// <summary>What this account may currently do. Drives the UI; never the gate itself.</summary>
[Route("api/billing/entitlements")]
public class GetEntitlements : IGet<EntitlementsDto>;

/// <summary>The account's subscription, as shown on the billing page.</summary>
[Route("api/billing/subscription")]
public class GetSubscription : IGet<SubscriptionDto>;

/// <summary>
/// The account's billing history, most recent first.
///
/// Paged rather than returned whole: this grows by a row every renewal, so a
/// long-lived monthly account accumulates them indefinitely.
/// </summary>
[Route("api/billing/history")]
public class GetBillingHistory : IGet<PaginatedResponse<SubscriptionEventDto>>
{
    [QueryStringParam] public int Page { get; set; } = 1;

    [QueryStringParam] public int PageSize { get; set; } = 10;
}

/// <summary>
/// Opens a checkout for a paid plan and returns where to send the browser.
/// The amount is decided server-side and recorded on the session.
/// </summary>
[Route("api/billing/checkout")]
public class StartCheckout : IPost<CheckoutSessionDto>
{
    [BodyParam] public BillingPlan Plan { get; set; } = BillingPlan.Pro;
    [BodyParam] public BillingCycle Cycle { get; set; } = BillingCycle.Monthly;
}

/// <summary>Details of an open checkout, for the payment page to display.</summary>
[Route("api/billing/checkout/{SessionId}")]
public class GetCheckout : IGet<CheckoutSessionDto>
{
    [RouteParam] public Guid SessionId { get; set; }
}

/// <summary>
/// Marks a checkout paid.
///
/// While billing is simulated this is called by the fake payment page. With a
/// real provider it is the webhook that calls it, and this endpoint goes away.
/// </summary>
[Route("api/billing/checkout/{SessionId}/complete")]
public class CompleteCheckout : IPost<CheckoutResultDto>
{
    [RouteParam] public Guid SessionId { get; set; }
}

/// <summary>Records that the free plan was chosen, so the picker stops appearing.</summary>
[Route("api/billing/select-free")]
public class SelectFreePlan : IPost<SubscriptionDto>;

/// <summary>Cancels at the end of the paid period.</summary>
[Route("api/billing/cancel")]
public class CancelSubscription : IPost<SubscriptionDto>;

/// <summary>Undoes a pending cancellation.</summary>
[Route("api/billing/resume")]
public class ResumeSubscription : IPost<SubscriptionDto>;
