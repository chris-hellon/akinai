using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Shared.Billing;
using InvoiceSoftware.Shared.Dtos.Billing;
using InvoiceSoftware.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Infrastructure.Services;

/// <inheritdoc />
public class EntitlementService(IDbContextFactory<ApplicationDbContext> dbFactory) : IEntitlementService
{
    public async Task<EntitlementsDto> GetAsync(string userId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var user = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.Plan,
                u.SubscriptionStatus,
                u.BillingCycle,
                u.CurrentPeriodEndUtc,
                u.PlanSelectedAtUtc,
            })
            .FirstOrDefaultAsync(cancellationToken);

        // An account we cannot identify gets the free tier, never a paid one.
        if (user is null)
            return Build(Plans.Free, BillingPlan.Free, SubscriptionStatus.None, null, null, false, 0,
                [], ["English"]);

        var effectivePlan = ResolveEffectivePlan(
            user.Plan, user.SubscriptionStatus, user.CurrentPeriodEndUtc, DateTime.UtcNow);

        var definition = Plans.For(effectivePlan);

        // Counted even on unlimited plans: the billing page reports usage as
        // well as enforcing it, and skipping the query there made an account
        // with invoices read "0 this month".
        var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var invoicesThisMonth = await db.Invoices
            .AsNoTracking()
            .CountAsync(i => i.Client.UserId == userId && i.CreatedAt >= startOfMonth, cancellationToken);

        // Restricted plans publish exactly what they may pick, from the same
        // rule the save path enforces — so a picker built from this can never
        // offer an option the server will refuse.
        var allowedCurrencies = definition.MultiCurrency
            ? null
            : (await CurrencyPolicy.InUseAsync(db, userId, cancellationToken)).OrderBy(c => c).ToList();

        var allowedLanguages = definition.AllLanguages ? null : new List<string> { "English" };

        return Build(
            definition,
            effectivePlan,
            user.SubscriptionStatus,
            user.BillingCycle,
            user.CurrentPeriodEndUtc,
            user.PlanSelectedAtUtc is not null,
            invoicesThisMonth,
            allowedCurrencies,
            allowedLanguages);
    }

    /// <summary>
    /// Works out what an account is entitled to, from what it has paid for and
    /// when that runs out.
    ///
    /// Time is authoritative here rather than status alone: there is no
    /// background job expiring subscriptions, so a period that has passed must
    /// degrade on read. Otherwise a cancelled account would keep its paid
    /// features indefinitely simply because nothing ran to change the row.
    /// </summary>
    public static BillingPlan ResolveEffectivePlan(
        BillingPlan storedPlan,
        SubscriptionStatus status,
        DateTime? currentPeriodEndUtc,
        DateTime utcNow)
        => status switch
        {
            // Both keep their entitlements to the end of the paid period —
            // cancelling is not a refund.
            SubscriptionStatus.Active or SubscriptionStatus.Cancelling
                when currentPeriodEndUtc is null || currentPeriodEndUtc > utcNow => storedPlan,

            // Expired, past due, never subscribed, or a period that has run out.
            _ => BillingPlan.Free,
        };

    private static EntitlementsDto Build(
        PlanDefinition definition,
        BillingPlan plan,
        SubscriptionStatus status,
        BillingCycle? cycle,
        DateTime? periodEnd,
        bool planSelected,
        int invoicesUsed,
        IReadOnlyList<string>? allowedCurrencies = null,
        IReadOnlyList<string>? allowedLanguages = null)
        => new(
            plan,
            definition.Name,
            status,
            cycle,
            periodEnd,
            planSelected,
            definition.InvoicesPerMonth,
            invoicesUsed,
            definition.MultiCurrency,
            definition.AllLanguages,
            definition.BankImport,
            definition.RecurringBilling,
            definition.Estimates,
            definition.CustomTemplates,
            definition.RemoveBranding,
            allowedCurrencies,
            allowedLanguages);
}
