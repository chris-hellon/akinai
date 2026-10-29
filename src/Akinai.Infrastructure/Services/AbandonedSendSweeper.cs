using Akinai.Domain.Enums;
using Akinai.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Akinai.Infrastructure.Services;

/// <summary>
/// Returns to draft any invoice whose send started and never finished.
///
/// Sending is two-phase: <c>SendInvoiceHandler</c> commits the status, then renders the
/// document and emails it, rolling the status back if either fails. The rollback is a
/// catch block, so it only runs if the process is still alive to run it. Kill the app
/// mid-send — a deploy, an OOM, a lost container — and the invoice stays marked Sent
/// while nobody ever received it.
///
/// <para>
/// What makes this safe is <see cref="Domain.Entities.Invoice.SendStartedAtUtc"/>. The
/// obvious signature for an interrupted send is "Sent with no delivery record", and it is
/// wrong: marking an invoice as sent by hand produces exactly that row, and is an ordinary
/// thing to do for an invoice posted or sent from the user's own mail client. Sweeping on
/// it would return invoices to draft that the user deliberately marked as sent — and on a
/// typical account most sent invoices look like that. Only the email path sets
/// SendStartedAtUtc, so only the email path can be swept, and every invoice predating this
/// field is invisible to it.
/// </para>
///
/// <para>
/// Rolling back rather than reporting, because the invoice is wrong in a way the user
/// cannot fix: Edit refuses on a non-draft, so an invoice stuck in this state can neither
/// be corrected nor legitimately re-sent. <see cref="Domain.Entities.Invoice.AbandonSend"/>
/// does the same thing the catch block would have, and keeps the same guard — anything
/// carrying a delivery record is refused outright, so a delivered invoice cannot be walked
/// back no matter what this sweeper believes.
/// </para>
/// </summary>
public class AbandonedSendSweeper(
    IServiceScopeFactory scopeFactory,
    ILogger<AbandonedSendSweeper> logger) : BackgroundService
{
    /// <summary>
    /// How long a send may legitimately be in flight. Generous on purpose: the render
    /// launches Chromium, which downloads itself on first use, and the email then waits on
    /// an SMTP server. Rolling back a send that was merely slow would be the one outcome
    /// worse than leaving it — the email lands, and the invoice says Draft.
    /// </summary>
    private static readonly TimeSpan InFlightGrace = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Every fifteen minutes. The condition only arises when the process dies, so this is
    /// about clearing it promptly after a restart rather than catching it as it happens.
    /// </summary>
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // A restart is exactly when there is something to find, so the first sweep runs
        // shortly after startup rather than after a full interval.
        try { await Task.Delay(TimeSpan.FromSeconds(40), stoppingToken); }
        catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                // One bad sweep must not stop every later one.
                logger.LogError(ex, "Abandoned-send sweep failed.");
            }
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken token)
    {
        try { return await timer.WaitForNextTickAsync(token); }
        catch (OperationCanceledException) { return false; }
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<ApplicationDbContext>>();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        await SweepAsync(db, DateTime.UtcNow, logger, cancellationToken);
    }

    /// <summary>
    /// The sweep itself, against a context and an explicit clock.
    ///
    /// Separated from the timer so it can be run against a real database in a test. What
    /// needs proving here is which rows it refuses to touch, and that is not something to
    /// establish by reading a LINQ expression.
    /// </summary>
    /// <returns>How many invoices were returned to draft.</returns>
    public static async Task<int> SweepAsync(
        ApplicationDbContext db,
        DateTime nowUtc,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var cutoff = nowUtc - InFlightGrace;

        var abandoned = await db.Invoices
            .Where(i => i.Status == InvoiceStatus.Sent
                        && i.SendStartedAtUtc != null
                        && i.SendStartedAtUtc < cutoff
                        // Belt and braces. AbandonSend refuses a delivered invoice anyway,
                        // and RecordDelivery clears SendStartedAtUtc, so this should never
                        // exclude anything — but the one mistake that matters here is
                        // un-sending an invoice a client is holding.
                        && i.SentAtUtc == null)
            .ToListAsync(cancellationToken);

        if (abandoned.Count == 0) return 0;

        foreach (var invoice in abandoned)
        {
            invoice.AbandonSend();

            // Warning rather than information: every row here is a send that died, and a
            // sweeper quietly cleaning up after a recurring crash is a bug staying hidden.
            logger.LogWarning(
                "Invoice {InvoiceNumber} ({InvoiceId}) was marked sent at {StartedAt:u} but never delivered; returned to draft.",
                invoice.InvoiceNumber, invoice.Id, invoice.SendStartedAtUtc);
        }

        await db.SaveChangesAsync(cancellationToken);
        return abandoned.Count;
    }
}
