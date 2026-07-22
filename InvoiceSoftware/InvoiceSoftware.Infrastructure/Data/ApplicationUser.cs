using InvoiceSoftware.Shared.Billing;
using Microsoft.AspNetCore.Identity;

namespace InvoiceSoftware.Infrastructure.Data;

public class ApplicationUser : IdentityUser
{
    /// <summary>
    /// The plan this account pays for. Not the same as what it is currently
    /// entitled to — a lapsed subscription keeps its plan recorded here while
    /// falling back to free entitlements. Resolve through IEntitlementService
    /// rather than reading this directly.
    /// </summary>
    public BillingPlan Plan { get; set; } = BillingPlan.Free;

    public SubscriptionStatus SubscriptionStatus { get; set; } = SubscriptionStatus.None;

    public BillingCycle? BillingCycle { get; set; }

    /// <summary>When the paid-for period ends. Entitlements survive until then, even after cancelling.</summary>
    public DateTime? CurrentPeriodEndUtc { get; set; }

    /// <summary>Set when the account has been asked to choose a plan, so the picker is shown once.</summary>
    public DateTime? PlanSelectedAtUtc { get; set; }

    /// <summary>Identifiers from the payment provider. Null while billing is simulated.</summary>
    public string? ProviderCustomerId { get; set; }
    public string? ProviderSubscriptionId { get; set; }
}
