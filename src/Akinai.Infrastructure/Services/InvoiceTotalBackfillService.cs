using Akinai.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Akinai.Infrastructure.Services;

/// <summary>
/// One-time background pass that snapshots totals for invoices created before the
/// multi-currency total feature (i.e. those with no <c>CurrencyTotals</c> rows yet).
/// Best-effort: failures are logged and skipped so they never block startup.
/// </summary>
public class InvoiceTotalBackfillService(
    IServiceScopeFactory scopeFactory,
    ILogger<InvoiceTotalBackfillService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Let the app finish starting (and the schema apply) before we touch the DB.
            await Task.Delay(TimeSpan.FromSeconds(12), stoppingToken);

            using var scope = scopeFactory.CreateScope();
            var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            var totalService = scope.ServiceProvider.GetRequiredService<IInvoiceTotalService>();

            await using var db = await dbFactory.CreateDbContextAsync(stoppingToken);

            var pending = await db.Invoices
                .Where(i => !i.CurrencyTotals.Any())
                .Select(i => new { i.Id, UserId = i.Client.UserId })
                .Take(2000)
                .ToListAsync(stoppingToken);

            var done = 0;
            foreach (var item in pending)
            {
                if (stoppingToken.IsCancellationRequested) break;
                try
                {
                    await totalService.RecalculateAsync(db, item.Id, item.UserId, stoppingToken);
                    await db.SaveChangesAsync(stoppingToken);
                    done++;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to backfill totals for invoice {InvoiceId}", item.Id);
                }
            }

            if (done > 0)
                logger.LogInformation("Backfilled multi-currency totals for {Count} invoices", done);
        }
        catch (OperationCanceledException)
        {
            // App shutting down.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Invoice total backfill pass failed");
        }
    }
}
