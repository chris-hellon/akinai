using Akinai.Domain.Entities;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Billing;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Akinai.Infrastructure.Services.Billing;

/// <inheritdoc />
public class PlanCatalog(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    IMemoryCache cache,
    ILogger<PlanCatalog> logger) : IPlanCatalog
{
    private const string CacheKey = "plan-catalogue";

    // Cached because this is read on nearly every authorised request — the
    // entitlement check runs before anything gated. A five minute ceiling is a
    // backstop only; an edit invalidates immediately via Invalidate().
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(5);

    public async Task<PlanDefinition> ForAsync(BillingPlan plan, CancellationToken cancellationToken = default)
    {
        var all = await AllAsync(cancellationToken);
        return all.FirstOrDefault(p => p.Plan == plan) ?? Plans.For(plan);
    }

    public async Task<IReadOnlyList<PlanDefinition>> AllAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(CacheKey, out IReadOnlyList<PlanDefinition>? cached) && cached is not null)
            return cached;

        IReadOnlyList<PlanDefinition> result;
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var rows = await db.PlanSettings.AsNoTracking()
                .OrderBy(p => p.SortOrder)
                .ToListAsync(cancellationToken);

            // An empty table means the seeder has not run yet. Fall back rather
            // than returning nothing, which would read as "no plan grants
            // anything" and lock every account out of its own features.
            result = rows.Count == 0
                ? Plans.All
                : rows.Select(ToDefinition).Where(p => p is not null).Select(p => p!).ToList();

            if (result.Count == 0) result = Plans.All;
        }
        catch (Exception ex)
        {
            // Never fail open. Compiled defaults are the safe answer.
            logger.LogError(ex, "Plan catalogue unavailable; falling back to the compiled defaults.");
            result = Plans.All;
        }

        cache.Set(CacheKey, result, CacheFor);
        return result;
    }

    public void Invalidate() => cache.Remove(CacheKey);

    /// <summary>
    /// Maps a stored row onto the definition the rest of the app consumes.
    /// Returns null for a tier the code does not know about — such a row could
    /// not be gated by anything, so it must not reach the pricing page either.
    /// </summary>
    private static PlanDefinition? ToDefinition(PlanSettings p)
    {
        if (!Enum.TryParse<BillingPlan>(p.Plan, ignoreCase: true, out var plan))
            return null;

        return new PlanDefinition(
            plan,
            p.Name,
            p.Tagline,
            p.MonthlyPrice,
            p.AnnualPrice,
            p.Currency,
            p.CurrencySymbol,
            p.InvoicesPerMonth,
            p.MultiCurrency,
            p.AllLanguages,
            p.BankImport,
            p.RecurringBilling,
            p.Estimates,
            p.CustomTemplates,
            p.RemoveBranding,
            p.ScanToPay,
            p.PaymentInsights,
            p.AiSpendLimitUsd,
            p.Highlights.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}
