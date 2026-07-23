using Akinai.Domain.Entities;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Ai;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Akinai.Infrastructure.Services;

/// <inheritdoc />
public class AiBudgetService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ILogger<AiBudgetService> logger,
    IPlanCatalog catalog) : IAiBudgetService
{
    public async Task<AiBudget> GetAsync(string userId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await BuildAsync(db, userId, cancellationToken);
    }

    public async Task<AiBudget> RecordTokensAsync(
        string userId, string feature, string model,
        int inputTokens, int outputTokens, CancellationToken cancellationToken = default)
    {
        var cost = AiPricing.ForTokens(model, inputTokens, outputTokens);
        var usage = AiUsage.ForTokens(userId, feature, model, inputTokens, outputTokens, cost);
        return await SaveAsync(userId, usage, cost, feature, cancellationToken);
    }

    public async Task<AiBudget> RecordTranslationAsync(
        string userId, string feature, int characters, CancellationToken cancellationToken = default)
    {
        var cost = AiPricing.ForTranslation(characters);
        var usage = AiUsage.ForTranslation(userId, feature, characters, cost);
        return await SaveAsync(userId, usage, cost, feature, cancellationToken);
    }

    private async Task<AiBudget> SaveAsync(
        string userId, AiUsage usage, decimal cost, string feature, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        db.AiUsages.Add(usage);
        await db.SaveChangesAsync(cancellationToken);

        var budget = await BuildAsync(db, userId, cancellationToken);

        // Worth knowing about before a customer complains: an account that has
        // burned its month in one sitting is either a power user worth talking
        // to or a loop worth finding.
        if (!budget.CanSpend && budget.HasAllowance)
        {
            logger.LogWarning(
                "AI budget exhausted for {UserId}: {Spent} of {Limit} USD (last call {Feature}, {Cost} USD).",
                userId, budget.SpentUsd, budget.LimitUsd, feature, cost);
        }

        return budget;
    }

    private async Task<AiBudget> BuildAsync(
        ApplicationDbContext db, string userId, CancellationToken cancellationToken)
    {
        var user = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Plan, u.SubscriptionStatus, u.CurrentPeriodEndUtc })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null) return new AiBudget(null, 0m);

        // Resolved, not stored: a lapsed Pro account loses its AI allowance the
        // same way it loses every other paid feature.
        var effectivePlan = EntitlementService.ResolveEffectivePlan(
            user.Plan, user.SubscriptionStatus, user.CurrentPeriodEndUtc, DateTime.UtcNow);
        var limit = (await catalog.ForAsync(effectivePlan, cancellationToken)).AiSpendLimitUsd;

        // Calendar month, matching how the invoice cap resets. Skipped entirely
        // when the plan has no allowance — nothing to compare against.
        if (limit is null or <= 0) return new AiBudget(limit, 0m);

        var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var spent = await db.AiUsages
            .AsNoTracking()
            .Where(u => u.UserId == userId && u.OccurredAtUtc >= startOfMonth)
            .SumAsync(u => (decimal?)u.CostUsd, cancellationToken) ?? 0m;

        return new AiBudget(limit, spent);
    }
}
