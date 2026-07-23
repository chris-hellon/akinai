using Akinai.Shared.Billing;

namespace Akinai.Shared.Dtos.Billing;

/// <summary>
/// What the signed-in account is allowed to do right now.
///
/// This is the resolved answer, not the stored one: a lapsed or past-due
/// subscription reports free-tier entitlements even though the account still
/// records the plan it used to be on.
/// </summary>
public record EntitlementsDto(
    BillingPlan Plan,
    string PlanName,
    SubscriptionStatus Status,
    BillingCycle? Cycle,
    DateTime? CurrentPeriodEndUtc,
    bool PlanSelected,
    int? InvoicesPerMonth,
    int InvoicesUsedThisMonth,
    bool MultiCurrency,
    bool AllLanguages,
    bool BankImport,
    bool RecurringBilling,
    bool Estimates,
    bool CustomTemplates,
    bool RemoveBranding,
    /// <summary>
    /// The currencies this account may use, or null when unrestricted.
    ///
    /// Comes from the same rule the server enforces, so a picker built from
    /// this cannot offer something the save will refuse. An empty list means
    /// restricted-but-undecided: the account has no currency yet and its next
    /// choice becomes the one.
    /// </summary>
    IReadOnlyList<string>? AllowedCurrencies,
    /// <summary>Languages this account may use, or null when unrestricted.</summary>
    IReadOnlyList<string>? AllowedLanguages,
    /// <summary>Monthly AI/translation allowance in USD; null when the plan has none.</summary>
    decimal? AiSpendLimitUsd = null,
    /// <summary>Month-to-date AI spend in USD.</summary>
    decimal AiSpentUsd = 0m)
{
    public bool IsPaid => Plan != BillingPlan.Free;

    /// <summary>Null when unlimited.</summary>
    public int? InvoicesRemaining => InvoicesPerMonth is int cap ? Math.Max(0, cap - InvoicesUsedThisMonth) : null;

    /// <summary>True when the plan includes AI features and budget remains.</summary>
    public bool CanUseAi => AiSpendLimitUsd is decimal limit && limit > 0 && AiSpentUsd < limit;

    public bool CanCreateInvoice => InvoicesPerMonth is not int cap || InvoicesUsedThisMonth < cap;
}

/// <summary>The account's subscription as shown on the billing page.</summary>
public record SubscriptionDto(
    BillingPlan Plan,
    string PlanName,
    SubscriptionStatus Status,
    BillingCycle? Cycle,
    DateTime? CurrentPeriodEndUtc,
    bool CancelAtPeriodEnd,
    decimal? RenewalAmount,
    string Currency,
    string CurrencySymbol,
    bool IsSimulated,
    string ProviderName);

/// <summary>
/// A started checkout. <see cref="RedirectUrl"/> is where the browser should
/// go next — an internal page while billing is simulated, and the provider's
/// hosted page once it is real.
/// </summary>
public record CheckoutSessionDto(
    Guid Id,
    BillingPlan Plan,
    string PlanName,
    BillingCycle Cycle,
    decimal Amount,
    string Currency,
    string CurrencySymbol,
    DateTime ExpiresAtUtc,
    bool IsSimulated,
    string RedirectUrl);

/// <summary>Outcome of completing a checkout.</summary>
public record CheckoutResultDto(bool Succeeded, string? Error, SubscriptionDto? Subscription);

/// <summary>One line of the account's billing history.</summary>
public record SubscriptionEventDto(
    DateTime OccurredAtUtc,
    string Type,
    string FromPlan,
    string ToPlan,
    decimal? Amount,
    string? Currency,
    bool Simulated,
    string? Note);
