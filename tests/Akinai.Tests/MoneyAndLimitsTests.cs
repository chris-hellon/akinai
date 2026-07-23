using FluentAssertions;
using Akinai.Shared.Ai;
using Akinai.Shared.Billing;
using Akinai.Shared.Dtos.Billing;
using Akinai.Shared.Services;

namespace Akinai.Tests;

/// <summary>
/// The arithmetic behind what someone is allowed to do and what it costs.
///
/// These are the calculations nobody checks by hand: an off-by-one on the
/// invoice cap either blocks a paying customer or gives away a free one, and a
/// rounding slip on the AI cap turns a fuse into a permanent block.
/// </summary>
public class MoneyAndLimitsTests
{
    private static EntitlementsDto Ents(int? cap, int used, decimal? aiLimit = null, decimal aiSpent = 0m) =>
        new(BillingPlan.Pro, "Pro", SubscriptionStatus.Active, BillingCycle.Monthly, null, true,
            cap, used, true, true, true, true, true, true, true, null, null, aiLimit, aiSpent);

    [Theory]
    [InlineData(5, 0, true)]
    [InlineData(5, 4, true)]   // the fifth invoice must still be allowed
    [InlineData(5, 5, false)]  // the sixth must not
    [InlineData(5, 9, false)]
    public void Invoice_cap_allows_exactly_the_advertised_number(int cap, int used, bool allowed)
        => Ents(cap, used).CanCreateInvoice.Should().Be(allowed);

    [Fact]
    public void No_cap_means_unlimited()
    {
        Ents(null, 10_000).CanCreateInvoice.Should().BeTrue();
        Ents(null, 10_000).InvoicesRemaining.Should().BeNull();
    }

    [Fact]
    public void Remaining_never_goes_negative()
    {
        // An account that was downgraded mid-month can sit above its new cap.
        // "-3 invoices remaining" would be shown to a user verbatim.
        Ents(5, 8).InvoicesRemaining.Should().Be(0);
    }

    // MemberData rather than InlineData: xUnit passes numeric literals as
    // double, which cannot convert to decimal? — and money must not be a double.
    public static TheoryData<decimal?, decimal, bool> AiBudgetCases => new()
    {
        { null,  0m,    false },  // no allowance at all
        { 1.00m, 0.00m, true  },
        { 1.00m, 0.99m, true  },  // a fraction left is still enough for one call
        { 1.00m, 1.00m, false },  // spent exactly the cap
        { 1.00m, 1.50m, false },
    };

    [Theory]
    [MemberData(nameof(AiBudgetCases))]
    public void Ai_is_allowed_only_while_budget_remains(decimal? limit, decimal spent, bool can)
        => Ents(null, 0, limit, spent).CanUseAi.Should().Be(can);

    [Fact]
    public void Zero_allowance_is_not_an_allowance()
    {
        // Distinct from null, and easy to get wrong: `limit > 0` rather than
        // `limit is not null` is what stops a 0.00 plan spending anything.
        Ents(null, 0, 0m, 0m).CanUseAi.Should().BeFalse();
    }

    [Fact]
    public void Budget_reports_remaining_without_going_negative()
    {
        new AiBudget(1.00m, 1.75m).RemainingUsd.Should().Be(0m);
        new AiBudget(1.00m, 0.25m).RemainingUsd.Should().Be(0.75m);
        new AiBudget(null, 5m).RemainingUsd.Should().Be(0m);
    }

    [Fact]
    public void Percent_used_is_clamped_to_a_hundred()
        => new AiBudget(1.00m, 3.00m).PercentUsed.Should().Be(100);

    [Fact]
    public void Annual_saving_is_derived_from_the_two_prices()
    {
        // £9/month against £90/year is two months free. Hardcoding "2 months"
        // anywhere would silently lie the first time a price changed.
        var pro = new PlanDefinition(BillingPlan.Pro, "Pro", "", 9m, 90m, "GBP", "£",
            null, true, true, true, true, true, true, true, 1m, []);

        pro.AnnualMonthsFree.Should().Be(2m);
        pro.PriceFor(BillingCycle.Annual).Should().Be(90m);
        pro.PriceFor(BillingCycle.Monthly).Should().Be(9m);
        pro.IsPaid.Should().BeTrue();
    }

    [Fact]
    public void A_free_plan_reports_no_annual_saving_and_does_not_divide_by_zero()
    {
        var free = new PlanDefinition(BillingPlan.Free, "Free", "", 0m, 0m, "GBP", "£",
            5, false, false, false, false, false, false, false, null, []);

        free.AnnualMonthsFree.Should().Be(0m);
        free.IsPaid.Should().BeFalse();
    }

    [Fact]
    public void Translation_is_priced_per_source_character()
        => AiPricing.ForTranslation(1_000_000).Should().Be(10.00m);

    [Fact]
    public void Token_pricing_uses_the_model_rate_and_ignores_unknown_models()
    {
        // Haiku: $1/M in, $5/M out.
        AiPricing.ForTokens(AiPricing.HaikuModel, 1_000_000, 0).Should().Be(1.00m);
        AiPricing.ForTokens(AiPricing.HaikuModel, 0, 1_000_000).Should().Be(5.00m);

        // An unrecognised model prices at zero rather than throwing — a bad
        // model name must not take down invoice generation.
        AiPricing.ForTokens("something-else", 1_000_000, 1_000_000).Should().Be(0m);
    }
}
