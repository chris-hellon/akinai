using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Admin;
using Akinai.Shared.Billing;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Admin;

public class GetPlanSettingsHandler(IPlanCatalog catalog) : IHandle<GetPlanSettings, List<PlanDefinition>>
{
    public async Task<HttpResult<List<PlanDefinition>>> Handle(GetPlanSettings request, CancellationToken cancellationToken)
        => HttpResult<List<PlanDefinition>>.Ok((await catalog.AllAsync(cancellationToken)).ToList());
}

public class SavePlanSettingsHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    IPlanCatalog catalog) : IHandle<SavePlanSettings>
{
    public async Task<HttpResult> Handle(SavePlanSettings request, CancellationToken cancellationToken)
    {
        // Only tiers the code knows about. A row for anything else could not be
        // gated, so it must not be creatable through the API either.
        if (!Enum.TryParse<BillingPlan>(request.Plan, ignoreCase: true, out var plan))
            return HttpResult.NotFound();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.PlanSettings.FirstOrDefaultAsync(
            p => p.Plan == plan.ToString(), cancellationToken);

        if (row is null) return HttpResult.NotFound();

        // A free tier that costs money, or a paid tier that is free, would make
        // the upgrade flow contradict itself — the checkout refuses unpaid plans.
        if (plan == BillingPlan.Free && request.MonthlyPrice > 0) return HttpResult.BadRequest();
        if (request.MonthlyPrice < 0 || request.AnnualPrice < 0) return HttpResult.BadRequest();
        if (request.InvoicesPerMonth is < 0) return HttpResult.BadRequest();

        row.Update(
            request.Name, request.Tagline,
            request.MonthlyPrice, request.AnnualPrice, request.Currency, request.CurrencySymbol,
            request.InvoicesPerMonth, request.MultiCurrency, request.AllLanguages, request.BankImport,
            request.RecurringBilling, request.Estimates, request.CustomTemplates, request.RemoveBranding,
            request.AiSpendLimitUsd, request.Highlights ?? "");

        await db.SaveChangesAsync(cancellationToken);

        // Drop the cache so the next entitlement check sees the new terms
        // rather than serving the old ones for up to five minutes.
        catalog.Invalidate();
        return HttpResult.Ok();
    }
}
