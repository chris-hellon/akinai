using Akinai.Domain.Entities;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Akinai.Infrastructure.Services.Billing;

/// <summary>
/// Puts a row in PlanSettings for any tier that does not have one yet.
///
/// Deliberately insert-only. It never updates an existing row, because that row
/// is what the administrator edited — a deploy that quietly reset the price back
/// to whatever was compiled in would be an unpleasant surprise. Adding a new
/// tier to the code seeds it on next start; changing an existing one is a
/// decision for whoever manages billing.
/// </summary>
public static class PlanSettingsSeeder
{
    public static async Task SeedAsync(ApplicationDbContext db, ILogger? logger = null, CancellationToken cancellationToken = default)
    {
        var existing = await db.PlanSettings
            .Select(p => p.Plan)
            .ToListAsync(cancellationToken);

        var added = 0;
        foreach (var (definition, index) in Plans.All.Select((d, i) => (d, i)))
        {
            var key = definition.Plan.ToString();
            if (existing.Contains(key, StringComparer.OrdinalIgnoreCase)) continue;

            db.PlanSettings.Add(PlanSettings.Create(
                key,
                definition.Name,
                definition.Tagline,
                definition.MonthlyPrice,
                definition.AnnualPrice,
                definition.Currency,
                definition.CurrencySymbol,
                definition.InvoicesPerMonth,
                definition.MultiCurrency,
                definition.AllLanguages,
                definition.BankImport,
                definition.RecurringBilling,
                definition.Estimates,
                definition.CustomTemplates,
                definition.RemoveBranding,
                definition.ScanToPay,
                definition.PaymentInsights,
                definition.AiSpendLimitUsd,
                string.Join('\n', definition.Highlights),
                index));
            added++;
        }

        if (added > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            logger?.LogInformation("Seeded {Count} plan settings row(s) from the compiled catalogue.", added);
        }
    }
}
