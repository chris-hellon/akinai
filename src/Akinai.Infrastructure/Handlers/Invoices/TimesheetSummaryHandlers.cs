using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.Enums;
using Akinai.Domain.Services;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Invoices;
using Akinai.Shared.Dtos.Invoices;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Akinai.Infrastructure.Handlers.Invoices;

/// <summary>
/// Loads the billable time on an invoice, once, for both the preview and the
/// save.
///
/// Shared so the two cannot disagree about what "every entry" means — if the
/// preview grouped a different set from the one the save validates against,
/// every save would be rejected, or worse, would not be.
/// </summary>
internal static class InvoiceTimesheet
{
    public sealed record Loaded(
        Domain.Entities.Invoice Invoice,
        IReadOnlyList<Domain.Entities.TimeEntry> Entries);

    /// <summary>Null when the invoice does not exist or is not the caller's.</summary>
    public static async Task<Loaded?> LoadAsync(
        ApplicationDbContext db, Guid invoiceId, string userId, CancellationToken cancellationToken)
    {
        var invoice = await db.Invoices
            .Include(i => i.Client)
            .Include(i => i.ServiceLines)
            .FirstOrDefaultAsync(
                i => i.Id == invoiceId && i.Client.UserId == userId, cancellationToken);

        if (invoice is null) return null;

        var entries = await db.TimeEntries
            .Include(e => e.Job).ThenInclude(j => j.Project)
            .Where(e => e.InvoiceId == invoiceId)
            .OrderBy(e => e.Date)
            .ToListAsync(cancellationToken);

        return new Loaded(invoice, entries);
    }

    /// <summary>The entries reduced to what grouping and pricing need.</summary>
    public static IReadOnlyCollection<TimesheetGrouping.BillableEntry> ToBillable(
        IEnumerable<Domain.Entities.TimeEntry> entries)
        => entries
            .Select(e => new TimesheetGrouping.BillableEntry(
                e.Id, e.BillableHours, e.Job.GetEffectiveHourlyRate()))
            .ToList();
}

/// <summary>Proposes a grouping. Writes nothing.</summary>
public class SummariseTimesheetHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService,
    ITimesheetSummariser summariser,
    ILogger<SummariseTimesheetHandler> logger)
    : IHandle<SummariseTimesheet, TimesheetSummaryDto>
{
    public async Task<HttpResult<TimesheetSummaryDto>> Handle(
        SummariseTimesheet request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<TimesheetSummaryDto>.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var loaded = await InvoiceTimesheet.LoadAsync(db, request.Id, userId, cancellationToken);

        if (loaded is null)
            return HttpResult<TimesheetSummaryDto>.NotFound();

        if (loaded.Invoice.Status != InvoiceStatus.Draft)
        {
            return HttpResult<TimesheetSummaryDto>.Ok(TimesheetSummaryDto.Unavailable(
                "This invoice has been issued, so its lines can no longer be changed."));
        }

        var entries = loaded.Entries;

        var summary = await summariser.SummariseAsync(
            userId,
            loaded.Invoice.Client.Name,
            entries.Select(e => new TimesheetEntry(
                e.Id,
                e.Job.Name,
                e.Job.Project.Name,
                e.Description,
                e.Date,
                e.BillableHours)).ToList(),
            request.Tone,
            cancellationToken);

        if (!summary.HasGroups)
        {
            return HttpResult<TimesheetSummaryDto>.Ok(
                TimesheetSummaryDto.Unavailable(summary.Reason ?? "No summary was produced.", entries.Count));
        }

        // The model's grouping, priced and checked. A grouping that does not
        // account for every entry exactly once is refused outright — see
        // TimesheetGrouping — and the user keeps their raw rows.
        var built = TimesheetGrouping.Build(
            loaded.Invoice.Id,
            InvoiceTimesheet.ToBillable(entries),
            summary.Groups
                .Select(g => new TimesheetGrouping.ProposedGroup(g.Description, g.TimeEntryIds))
                .ToList());

        if (!built.Accepted)
        {
            logger.LogWarning(
                "Rejected a proposed grouping for invoice {InvoiceId}: {Reason}",
                request.Id, built.Rejection);

            return HttpResult<TimesheetSummaryDto>.Ok(TimesheetSummaryDto.Unavailable(
                "The summary did not account for every entry, so it was discarded. Please try again.",
                entries.Count));
        }

        // Pair each built line back to the entries it came from, so the client
        // can send the same grouping back on save.
        var lines = built.Lines
            .Select((line, i) => new ProposedServiceLineDto(
                line.Description,
                summary.Groups[i].TimeEntryIds.ToList(),
                line.Hours,
                line.Amount))
            .ToList();

        return HttpResult<TimesheetSummaryDto>.Ok(
            new TimesheetSummaryDto(true, null, lines, entries.Count));
    }
}

/// <summary>Stores a reviewed summary.</summary>
public class SaveInvoiceServiceLinesHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService)
    : IHandle<SaveInvoiceServiceLines>
{
    public async Task<HttpResult> Handle(
        SaveInvoiceServiceLines request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var loaded = await InvoiceTimesheet.LoadAsync(db, request.Id, userId, cancellationToken);

        if (loaded is null) return HttpResult.NotFound();

        // Re-validated and re-priced here, not trusted from the request. The
        // client sends groupings and wording; it never sends money.
        var built = TimesheetGrouping.Build(
            loaded.Invoice.Id,
            InvoiceTimesheet.ToBillable(loaded.Entries),
            request.Lines
                .Select(l => new TimesheetGrouping.ProposedGroup(l.Description, l.TimeEntryIds))
                .ToList());

        if (!built.Accepted)
            return HttpResult.BadRequest();

        try
        {
            loaded.Invoice.ReplaceServiceLines(built.Lines);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Domain.Exceptions.DomainException)
        {
            // Non-draft. The domain refuses; so do we.
            return HttpResult.BadRequest();
        }

        return HttpResult.NoContent();
    }
}

/// <summary>Removes the summary, restoring the raw rows.</summary>
public class ClearInvoiceServiceLinesHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService)
    : IHandle<ClearInvoiceServiceLines>
{
    public async Task<HttpResult> Handle(
        ClearInvoiceServiceLines request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var invoice = await db.Invoices
            .Include(i => i.Client)
            .Include(i => i.ServiceLines)
            .FirstOrDefaultAsync(
                i => i.Id == request.Id && i.Client.UserId == userId, cancellationToken);

        if (invoice is null) return HttpResult.NotFound();

        try
        {
            invoice.ClearServiceLines();
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Domain.Exceptions.DomainException)
        {
            return HttpResult.BadRequest();
        }

        return HttpResult.NoContent();
    }
}
