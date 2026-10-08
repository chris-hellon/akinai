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
    ICurrentUserService currentUserService) : IHandle<GetInvoiceEvidence, InvoiceEvidenceDto?>
{
    public async Task<HttpResult<InvoiceEvidenceDto?>> Handle(
        GetInvoiceEvidence request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult<InvoiceEvidenceDto?>.Unauthorized();

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
            .Select(s => new { s.SignedAt, s.SignerName })
            .FirstOrDefaultAsync(cancellationToken);

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
            signature?.SignerName));
    }
}
