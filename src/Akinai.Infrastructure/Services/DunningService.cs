using Akinai.Domain.Entities;
using Akinai.Domain.Enums;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Dunning;
using Akinai.Shared.Geography;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Akinai.Infrastructure.Services;

/// <summary>
/// Sends the overdue reminders that <see cref="DunningSchedule"/> decides are due.
///
/// Three things govern everything here, and each exists because the failure it prevents is
/// one a client would notice before the user did:
///
/// <list type="bullet">
/// <item>Nothing is sent unless the account has explicitly switched reminders on. Emailing
/// somebody's clients in their name is not a default.</item>
/// <item>A send is recorded only after the email has gone. Recording first and failing
/// would silently skip a rung; recording late and crashing would send the same email
/// twice. The record is written immediately afterwards, so the worst case is a repeat
/// rather than a gap, and a repeat is at least visible.</item>
/// <item>The schedule is asked once per invoice with the full history, including other
/// invoices for the same client, so a client with four late invoices gets one email rather
/// than four.</item>
/// </list>
/// </summary>
public class DunningService(
    IServiceScopeFactory scopeFactory,
    ILogger<DunningService> logger) : BackgroundService
{
    /// <summary>
    /// Hourly. The schedule only sends inside the client's working hours and at most one
    /// reminder a day, so sweeping more often buys nothing; less often risks missing the
    /// window entirely for a client whose working day is short in our terms.
    /// </summary>
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(25), stoppingToken); }
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
                logger.LogError(ex, "Dunning sweep failed.");
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
        var sp = scope.ServiceProvider;

        var dbFactory = sp.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        var entitlements = sp.GetRequiredService<IEntitlementService>();
        var emailSender = sp.GetRequiredService<IInvoiceEmailSender>();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Only accounts that asked for this.
        var enabledUserIds = await db.BusinessProfiles
            .Where(b => b.AutomaticRemindersEnabled)
            .Select(b => b.UserId)
            .ToListAsync(cancellationToken);

        if (enabledUserIds.Count == 0) return;

        var now = DateTimeOffset.UtcNow;

        foreach (var userId in enabledUserIds)
        {
            if (cancellationToken.IsCancellationRequested) return;

            // Chasing is a paid feature, and the plan is checked per sweep rather than
            // cached: a lapsed subscription should stop sending that hour, not next restart.
            //
            // Gated on the named entitlement rather than on IsPaid, so an admin can
            // withdraw automatic sending from a tier without having to make that tier
            // free — the flag is what the settings page offers the toggle on, too.
            var plan = await entitlements.GetAsync(userId, cancellationToken);
            if (!plan.AutomaticReminders) continue;

            await SweepUserAsync(db, emailSender, userId, now, cancellationToken);
        }
    }

    private async Task SweepUserAsync(
        ApplicationDbContext db,
        IInvoiceEmailSender emailSender,
        string userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var candidates = await db.Invoices
            .Include(i => i.Client)
            .Where(i => i.Client.UserId == userId
                        && (i.Status == InvoiceStatus.Sent || i.Status == InvoiceStatus.Overdue))
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0) return;

        var invoiceIds = candidates.Select(i => i.Id).ToList();
        var clientIds = candidates.Select(i => i.ClientId).Distinct().ToList();

        var history = await db.DunningReminders
            .Where(r => invoiceIds.Contains(r.InvoiceId))
            .ToListAsync(cancellationToken);

        // Everything this user has sent any of these clients in the last day, so the
        // one-per-client-per-day rule can see across invoices.
        var since = now.UtcDateTime.AddDays(-1);
        var recentPerClient = await db.DunningReminders
            .Where(r => r.UserId == userId && clientIds.Contains(r.ClientId) && r.SentAtUtc >= since)
            .Select(r => new { r.ClientId, r.SentAtUtc })
            .ToListAsync(cancellationToken);

        foreach (var invoice in candidates)
        {
            if (cancellationToken.IsCancellationRequested) return;

            var email = invoice.Client.Email.Value;
            if (string.IsNullOrWhiteSpace(email)) continue;

            var alreadySent = history
                .Where(r => r.InvoiceId == invoice.Id)
                .Select(r => new DunningSendRecord(
                    Enum.Parse<DunningStage>(r.Stage),
                    new DateTimeOffset(r.SentAtUtc, TimeSpan.Zero)))
                .ToList();

            var otherContact = recentPerClient
                .Where(r => r.ClientId == invoice.ClientId)
                .Select(r => new DateTimeOffset(r.SentAtUtc, TimeSpan.Zero))
                .ToList();

            var zone = ClientTimeZone.FromOffset(
                CountryTimeZones.OffsetFor(invoice.Client.BillingAddress?.CountryCode));

            var decision = DunningSchedule.Evaluate(
                invoice.DueDate, now, zone, alreadySent, otherContact);

            if (!decision.ShouldSend || decision.Stage is not { } stage) continue;

            var language = DunningLanguage(invoice);
            var daysPastDue = DateOnly.FromDateTime(now.UtcDateTime).DayNumber - invoice.DueDate.DayNumber;

            var message = DunningTemplates.Render(
                language,
                stage,
                invoice.InvoiceNumber,
                $"{invoice.Currency} {invoice.Total:N2}",
                invoice.DueDate.ToString("d MMMM yyyy"),
                daysPastDue);

            try
            {
                await emailSender.SendEmailAsync(email, message.Subject, AsHtml(message.Body));
            }
            catch (Exception ex)
            {
                // No record is written, so this invoice is simply reconsidered next sweep.
                logger.LogWarning(ex,
                    "Could not send the {Stage} reminder for invoice {InvoiceId}.", stage, invoice.Id);
                continue;
            }

            var record = DunningReminder.Record(
                invoice.Id, invoice.ClientId, userId,
                stage.ToString(), now.UtcDateTime, email, language.ToString());

            db.DunningReminders.Add(record);
            await db.SaveChangesAsync(cancellationToken);

            // Kept in memory too, so a second invoice for the same client later in this
            // same sweep already sees that they have been contacted today.
            history.Add(record);
            recentPerClient.Add(new { invoice.ClientId, SentAtUtc = now.UtcDateTime });

            logger.LogInformation(
                "Sent the {Stage} reminder for invoice {InvoiceNumber}.", stage, invoice.InvoiceNumber);
        }
    }

    /// <summary>
    /// The language the invoice itself went out in, so the reminder matches the document
    /// it is chasing. Falls back to English, which is what the templates do anyway.
    /// </summary>
    private static Akinai.Shared.Enums.SupportedLanguage DunningLanguage(Invoice invoice)
        => invoice.Language is { } domainLanguage
            ? (Akinai.Shared.Enums.SupportedLanguage)(int)domainLanguage
            : Akinai.Shared.Enums.SupportedLanguage.English;

    /// <summary>
    /// The templates are plain text — deliberately, since they have to read well in eight
    /// languages — so line breaks become paragraphs here rather than in the wording.
    /// </summary>
    private static string AsHtml(string body)
    {
        var escaped = System.Net.WebUtility.HtmlEncode(body);
        return $"<div style=\"font-family:system-ui,-apple-system,sans-serif;font-size:14px;line-height:1.6\">{escaped.Replace("\n", "<br>")}</div>";
    }
}
