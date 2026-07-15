using InvoiceSoftware.Domain.Enums;
using InvoiceSoftware.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InvoiceSoftware.Infrastructure.Services;

/// <summary>
/// Periodically transitions <see cref="InvoiceStatus.Sent"/> invoices whose due date has
/// passed to <see cref="InvoiceStatus.Overdue"/>, so the stored status stays accurate for
/// status-based views (badges, filters, status breakdown). The dashboard's Overdue/Outstanding
/// figures are derived from the due date directly and don't depend on this running.
/// </summary>
public class OverdueInvoiceService(
    IServiceScopeFactory scopeFactory,
    ILogger<OverdueInvoiceService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the app finish starting (and the schema apply) before the first sweep.
        try { await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken); }
        catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(Interval);
        do
        {
            await SweepAsync(stoppingToken);
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken token)
    {
        try { return await timer.WaitForNextTickAsync(token); }
        catch (OperationCanceledException) { return false; }
    }

    private async Task SweepAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var db = await dbFactory.CreateDbContextAsync(stoppingToken);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var updated = await db.Invoices
                .Where(i => i.Status == InvoiceStatus.Sent && i.DueDate < today)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(i => i.Status, InvoiceStatus.Overdue),
                    stoppingToken);

            if (updated > 0)
                logger.LogInformation("Marked {Count} sent invoice(s) as overdue", updated);
        }
        catch (OperationCanceledException)
        {
            // App shutting down.
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Overdue invoice sweep failed");
        }
    }
}
