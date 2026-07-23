using FluentAssertions;
using Akinai.Infrastructure.Services;
using Akinai.Shared.Billing;

namespace Akinai.Tests;

/// <summary>
/// Which plan an account is actually on.
///
/// This is the highest-consequence pure function in the codebase: it decides
/// whether someone who has stopped paying still gets paid features, and whether
/// someone who has paid loses them early. Both failures are expensive and
/// neither is visible without looking — the UI would simply show the wrong
/// thing and nobody would report it.
/// </summary>
public class EntitlementResolutionTests
{
    private static readonly DateTime Now = new(2026, 7, 23, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Active_subscription_keeps_its_plan()
    {
        EntitlementService.ResolveEffectivePlan(
            BillingPlan.Pro, SubscriptionStatus.Active, Now.AddDays(7), Now)
            .Should().Be(BillingPlan.Pro);
    }

    [Fact]
    public void Cancelling_keeps_its_plan_until_the_period_ends()
    {
        // Cancelling is not a refund — they paid for the month, they get the month.
        EntitlementService.ResolveEffectivePlan(
            BillingPlan.Pro, SubscriptionStatus.Cancelling, Now.AddDays(3), Now)
            .Should().Be(BillingPlan.Pro);
    }

    [Fact]
    public void Cancelling_drops_to_free_once_the_period_has_passed()
    {
        EntitlementService.ResolveEffectivePlan(
            BillingPlan.Pro, SubscriptionStatus.Cancelling, Now.AddDays(-1), Now)
            .Should().Be(BillingPlan.Free);
    }

    [Fact]
    public void Active_but_expired_period_drops_to_free()
    {
        // The status alone is not enough. A row left as Active with a period end
        // in the past — a webhook that never arrived — must not grant Pro.
        EntitlementService.ResolveEffectivePlan(
            BillingPlan.Pro, SubscriptionStatus.Active, Now.AddSeconds(-1), Now)
            .Should().Be(BillingPlan.Free);
    }

    [Theory]
    [InlineData(SubscriptionStatus.PastDue)]
    [InlineData(SubscriptionStatus.Expired)]
    [InlineData(SubscriptionStatus.None)]
    public void Non_paying_statuses_drop_to_free_even_with_a_future_period(SubscriptionStatus status)
    {
        EntitlementService.ResolveEffectivePlan(
            BillingPlan.Pro, status, Now.AddYears(1), Now)
            .Should().Be(BillingPlan.Free);
    }

    [Fact]
    public void Active_with_no_period_end_keeps_its_plan()
    {
        // A null period end means open-ended, not expired. Reading null as
        // "already lapsed" would silently downgrade every such account.
        EntitlementService.ResolveEffectivePlan(
            BillingPlan.Pro, SubscriptionStatus.Active, null, Now)
            .Should().Be(BillingPlan.Pro);
    }

    [Fact]
    public void Free_stays_free_whatever_the_status_says()
    {
        EntitlementService.ResolveEffectivePlan(
            BillingPlan.Free, SubscriptionStatus.Active, Now.AddYears(1), Now)
            .Should().Be(BillingPlan.Free);
    }
}
