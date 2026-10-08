using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.Entities;
using Akinai.Infrastructure.Data;
using Akinai.Infrastructure.Services;
using Akinai.Shared.Api.Invoices;
using Akinai.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Akinai.Infrastructure.Handlers.Invoices;

public class SendInvoiceHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService,
    EmailTemplateService emailTemplateService,
    PdfGeneratorService pdfService,
    IExchangeRateService exchangeRates,
    IHttpContextAccessor httpContextAccessor,
    IInvoiceEmailSender emailSender,
    INotificationService notifications,
    Services.Pdf.IHtmlPdfRenderer htmlRenderer,
    Microsoft.Extensions.Logging.ILogger<SendInvoiceHandler> logger) : IHandle<SendInvoice>
{
    public async Task<HttpResult> Handle(SendInvoice request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var invoice = await db.Invoices
            .Include(i => i.Client)
            .Include(i => i.Template)
            .FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken);

        if (invoice is null) return HttpResult.NotFound();

        var timeEntries = await db.TimeEntries
            .Include(e => e.Job)
                .ThenInclude(j => j.Project)
                    .ThenInclude(p => p.Client)
            .Include(e => e.Job)
                .ThenInclude(j => j.Section)
            .Where(e => e.InvoiceId == request.Id)
            // Chronological, so the services table reads as the work happened —
            // and reads identically in the view, the PDF and the client's copy.
            // CreatedAt breaks ties so same-day entries cannot come back in a
            // different order from one query to the next.
            .OrderBy(e => e.Date)
            .ThenBy(e => e.CreatedAt)
            .ToListAsync(cancellationToken);

        var expenses = await db.Expenses
            .Where(e => e.InvoiceId == request.Id)
            // Same chronological rule as the services rows above.
            .OrderBy(e => e.ExpenseDate)
            .ThenBy(e => e.CreatedAt)
            .ToListAsync(cancellationToken);

        var productLineItems = await db.InvoiceLineItems
            .Include(li => li.Product)
            .Where(li => li.InvoiceId == request.Id)
            .OrderBy(li => li.Order)
            .ToListAsync(cancellationToken);

        // Wording this corridor requires on the document. The emailed PDF is the copy the
        // client actually holds, so it must carry it.
        var compliance = await InvoiceCompliance.AssessAsync(db, invoice.Id, invoice.CreatedBy, cancellationToken);

        // The snapshot total, so the figure in the email body matches the invoice
        // everywhere else. The rows above are still needed to render the PDF.
        var total = invoice.Total;

        // Resolve the sender's business profile (company name for the email,
        // plus branding/currency settings for the PDF).
        var userId = currentUserService.UserId;
        var companyName = "Your Company";
        Domain.Entities.BusinessProfile? businessProfile = null;
        Domain.Entities.CurrencyPaymentSettings? currencySettings = null;
        if (!string.IsNullOrEmpty(userId))
        {
            businessProfile = await db.BusinessProfiles
                .FirstOrDefaultAsync(b => b.UserId == userId, cancellationToken);
            currencySettings = await db.CurrencyPaymentSettings
                .FirstOrDefaultAsync(c => c.UserId == userId && c.CurrencyCode == invoice.Currency, cancellationToken);
            if (businessProfile != null)
            {
                companyName = (string.IsNullOrWhiteSpace(businessProfile.TradingName) ? businessProfile.CompanyName : businessProfile.TradingName) ?? companyName;
            }
        }

        // Build the public "view online" URL if the invoice has a public token.
        string? publicViewUrl = null;
        if (invoice.PublicAccessToken.HasValue && httpContextAccessor.HttpContext != null)
        {
            var httpRequest = httpContextAccessor.HttpContext.Request;
            var baseUrl = $"{httpRequest.Scheme}://{httpRequest.Host}";
            publicViewUrl = $"{baseUrl}/invoice/view/{invoice.PublicAccessToken}";
        }

        // --- Sending, in two phases -----------------------------------------
        // Phase one commits the status. It has to be committed rather than merely held in
        // memory, because the PDF below is printed from the invoice's own page over a
        // loopback request, and that request loads the invoice through a different context
        // which cannot see an uncommitted change. Without this the client is emailed an
        // invoice stamped "Draft".
        //
        // Phase two is the render and the email. If either fails the status is rolled back
        // by AbandonSend, which is permitted precisely because no delivery has been
        // recorded yet — so a failed attempt leaves no trace, exactly as before, while the
        // emailed document is now the same one the client sees online.
        invoice.Send();
        await db.SaveChangesAsync(cancellationToken);

        byte[] pdfBytes;
        try
        {
            // Render the user's message body through the template service so
            // {InvoiceDetails} and line breaks become proper HTML.
            var html = emailTemplateService.GenerateInvoiceEmailHtml(
                invoice, total, companyName, request.Body, publicViewUrl);

            var vndRate = await VndPaymentRate.GetAsync(exchangeRates, businessProfile?.DefaultCurrency, cancellationToken);

            // Print the page the client would open, falling back to rebuilding it. The
            // same composer the download path uses, so the two cannot diverge — which is
            // how a corridor-required tax statement once went out missing.
            pdfBytes = await InvoicePdfComposition.RenderAsync(
                invoice.Id,
                invoice.PublicAccessToken,
                htmlRenderer,
                httpContextAccessor,
                logger,
                fallback: () => pdfService.GenerateInvoicePdfAsync(
                    invoice, timeEntries, expenses, productLineItems, businessProfile, currencySettings,
                    invoice.Template, vndRate,
                    requiredStatements: compliance.RequiredStatements.ToList()),
                cancellationToken);

            var attachments = new List<EmailAttachment>();
            if (pdfBytes.Length > 0)
                attachments.Add(new EmailAttachment($"{invoice.InvoiceNumber}.pdf", pdfBytes, "application/pdf"));

            await emailSender.SendEmailAsync(request.Email, request.Subject, html, attachments);
        }
        catch
        {
            // Nothing reached the client, so the invoice goes back to draft. Rolled back on
            // its own context: the one above may be in an unusable state after a failure.
            await using var rollback = await dbFactory.CreateDbContextAsync(cancellationToken);
            var stale = await rollback.Invoices.FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken);
            if (stale is not null && stale.SentAtUtc is null)
            {
                stale.AbandonSend();
                await rollback.SaveChangesAsync(cancellationToken);
            }

            logger.LogWarning("Invoice {InvoiceId}: send failed and the invoice was returned to draft.", invoice.Id);
            throw;
        }

        // Only now, after the send succeeded: a delivery record for an email that failed
        // would be worse than none, because it would be believed.
        invoice.RecordDelivery(
            DateTime.UtcNow,
            request.Email,
            Akinai.Domain.Services.DocumentFingerprint.Of(pdfBytes));

        await db.SaveChangesAsync(cancellationToken);

        await notifications.CreateAsync(
            invoice.Client?.UserId ?? userId,
            Domain.Enums.NotificationType.InvoiceSent,
            "Invoice sent",
            $"{invoice.InvoiceNumber} sent to {invoice.Client?.Name ?? request.Email}",
            link: $"/invoices/{invoice.Id}",
            relatedEntityType: Domain.Enums.LinkedEntityType.Invoice,
            relatedEntityId: invoice.Id,
            cancellationToken: cancellationToken);

        return HttpResult.NoContent();
    }

}
