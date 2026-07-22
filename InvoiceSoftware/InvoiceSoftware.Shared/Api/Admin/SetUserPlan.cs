using BlazorUtils.EasyApi;
using InvoiceSoftware.Shared.Billing;

namespace InvoiceSoftware.Shared.Api.Admin;

/// <summary>
/// Sets an account's plan by hand, without payment.
///
/// This is how subscriptions get sold before real billing exists — invoice the
/// customer yourself, then grant the plan here. Every change is recorded in the
/// billing history against the admin who made it.
/// </summary>
[Route("api/admin/users/{UserId}/plan")]
public class SetUserPlan : IPost
{
    [RouteParam] public string UserId { get; set; } = "";
    [BodyParam] public BillingPlan Plan { get; set; } = BillingPlan.Free;
    [BodyParam] public BillingCycle? Cycle { get; set; }

    /// <summary>How long to grant, in months. Defaults to one billing cycle.</summary>
    [BodyParam] public int? Months { get; set; }
}
