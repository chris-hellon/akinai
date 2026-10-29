using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.Enums;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Invoices;
using Akinai.Shared.Dtos.Evidence;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Invoices;

/// <summary>
/// Assembles one invoice's chain of custody. Read-only, and scoped to the owner — an
/// evidence trail is about the owner's own document, and a token is not a licence to read
/// somebody else's.
/// </summary>
public class GetInvoiceEvidenceHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService,
    IEntitlementService entitlements) : IHandle<GetInvoiceEvidence, InvoiceEvidenceDto?>
{
    public async Task<HttpResult<InvoiceEvidenceDto?>> Handle(
        GetInvoiceEvidence request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult<InvoiceEvidenceDto?>.Unauthorized();

        // Checked here rather than only in the UI. The in-app page and the print route
        // both come through this handler, and an endpoint that trusts its callers is not
        // gated at all — /api/invoices/{id}/evidence is reachable with a cookie and curl.
        //
        // Resolved per request, so a lapsed subscription stops answering immediately
        // rather than at the next restart, and keyed on the named entitlement so a tier
        // can have the record withdrawn without being made free.
        var plan = await entitlements.GetAsync(userId, cancellationToken);
        if (!plan.DeliveryRecord) return HttpResult<InvoiceEvidenceDto?>.Forbidden();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var invoice = await db.Invoices
            .AsNoTracking()
            .Include(i => i.Client)
            .FirstOrDefaultAsync(i => i.Id == request.Id && i.Client.UserId == userId, cancellationToken);

        if (invoice is null) return HttpResult<InvoiceEvidenceDto?>.NotFound();

        // The signature, if the client signed. Most recent valid one wins.
        var signature = await db.DigitalSignatures
            .AsNoTracking()
            .Where(s => s.LinkedEntityType == LinkedEntityType.Invoice
                        && s.LinkedEntityId == invoice.Id
                        && s.IsValid)
            .OrderByDescending(s => s.SignedAt)
            .Select(s => new { s.SignedAt, s.SignerName, s.SignerEmail, s.SignerIpAddress })
            .FirstOrDefaultAsync(cancellationToken);

        // Oldest first, so the trail reads as the chase happened. SentAtUtc is the record
        // of fact — a reminder row exists only once its email has actually gone out.
        var reminders = await db.DunningReminders
            .AsNoTracking()
            .Where(r => r.InvoiceId == invoice.Id)
            .OrderBy(r => r.SentAtUtc)
            .Select(r => new InvoiceEvidenceReminderDto(r.Stage, r.SentAtUtc, r.ToEmail, r.Language))
            .ToListAsync(cancellationToken);

        // Re-assessed rather than stored. Honest about what it is: this is the treatment
        // that applies to the invoice as it stands now, which is the same thing the
        // document printed unless the client's country or registration has since changed.
        var compliance = await InvoiceCompliance.AssessAsync(db, invoice.Id, userId, cancellationToken);

        return HttpResult<InvoiceEvidenceDto?>.Ok(new InvoiceEvidenceDto(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.Client.Name,
            invoice.Currency,
            invoice.Total,
            invoice.IssueDate,
            invoice.DueDate,
            invoice.Status.ToString(),
            invoice.SentAtUtc,
            invoice.SentToEmail,
            invoice.SentDocumentSha256,
            invoice.FirstViewedAtUtc,
            invoice.LastViewedAtUtc,
            invoice.ViewCount,
            compliance.Treatment.ToString(),
            compliance.RequiredStatements,
            signature?.SignedAt,
            signature?.SignerName,
            signature?.SignerEmail,
            signature?.SignerIpAddress,
            reminders));
    }
}
