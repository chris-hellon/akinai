namespace InvoiceSoftware.Shared.Billing;

/// <summary>Which tier an account is on.</summary>
public enum BillingPlan
{
    Free = 0,
    Pro = 1,
}

/// <summary>
/// Where an account sits in the subscription lifecycle.
///
/// <see cref="Cancelling"/> is not the same as <see cref="Expired"/>: a
/// cancelled subscription keeps its entitlements until the end of the period
/// that has already been paid for.
/// </summary>
public enum SubscriptionStatus
{
    /// <summary>Never subscribed — on the free tier.</summary>
    None = 0,
    Active = 1,
    /// <summary>Cancelled, but still entitled until the period ends.</summary>
    Cancelling = 2,
    /// <summary>The period ended; entitlements have fallen back to free.</summary>
    Expired = 3,
    /// <summary>Payment failed. Entitlements fall back to free until it is fixed.</summary>
    PastDue = 4,
}

public enum BillingCycle
{
    Monthly = 0,
    Annual = 1,
}
