using FluentAssertions;
using Akinai.Shared.Billing;
using Akinai.Shared.Dtos.Billing;

namespace Akinai.Tests;

/// <summary>
/// AI features have three states, not two, and each needs different words.
///
/// A Free account has no allowance and should be offered an upgrade. A Pro
/// account that has spent this month's should be told it resets. Collapsing
/// the two — which a single CanUseAi flag does — tells a Free user that an
/// allowance they never had is "used up".
/// </summary>
public class AiEntitlementTests
{
    private static EntitlementsDto With(BillingPlan plan, decimal? limit, decimal spent) => new(
        Plan: plan,
        PlanName: plan.ToString(),
        Status: SubscriptionStatus.Active,
        Cycle: BillingCycle.Monthly,
        CurrentPeriodEndUtc: null,
        PlanSelected: true,
        InvoicesPerMonth: null,
        InvoicesUsedThisMonth: 0,
        MultiCurrency: true,
        AllLanguages: true,
        BankImport: true,
        RecurringBilling: true,
        Estimates: true,
        CustomTemplates: true,
        RemoveBranding: true,
        AllowedCurrencies: null,
        AllowedLanguages: null,
        AiSpendLimitUsd: limit,
        AiSpentUsd: spent);

    [Fact]
    public void Free_has_no_ai_at_all()
    {
        var free = With(BillingPlan.Free, limit: null, spent: 0m);

        free.HasAiFeatures.Should().BeFalse();
        free.CanUseAi.Should().BeFalse();
        // Must NOT read as "exhausted" — there was never an allowance.
        free.AiAllowanceExhausted.Should().BeFalse();
    }

    [Fact]
    public void Pro_with_allowance_remaining_can_use_ai()
    {
        var pro = With(BillingPlan.Pro, limit: 1.33m, spent: 0.04m);

        pro.HasAiFeatures.Should().BeTrue();
        pro.CanUseAi.Should().BeTrue();
        pro.AiAllowanceExhausted.Should().BeFalse();
    }

    [Fact]
    public void Pro_that_has_spent_the_allowance_is_exhausted_not_unentitled()
    {
        var pro = With(BillingPlan.Pro, limit: 1.33m, spent: 1.33m);

        pro.HasAiFeatures.Should().BeTrue();     // still a Pro perk
        pro.CanUseAi.Should().BeFalse();         // just not right now
        pro.AiAllowanceExhausted.Should().BeTrue();
    }

    [Fact]
    public void A_zero_allowance_counts_as_no_ai()
    {
        // An admin setting the limit to 0 means "off", not "off by a rounding".
        var off = With(BillingPlan.Pro, limit: 0m, spent: 0m);

        off.HasAiFeatures.Should().BeFalse();
        off.AiAllowanceExhausted.Should().BeFalse();
    }
}
