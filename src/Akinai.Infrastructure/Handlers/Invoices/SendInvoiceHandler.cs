using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.Entities;
using Akinai.Infrastructure.Data;
using Akinai.Infrastructure.Services;
using Akinai.Shared.Api.Invoices;
using Akinai.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Invoices;

public class SendInvoiceHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService,
    EmailTemplateService emailTemplateService,
    PdfGeneratorService pdfService,
    IExchangeRateService exchangeRates,
    IHttpContextAccessor httpContextAccessor,
    IInvoiceEmailSender emailSender,
    INotificationService notifications) : IHandle<SendInvoice>
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

        // Mark as sent up front so the generated PDF reflects the "Sent" status.
        // The change is only persisted after the email succeeds (below), so a
        // send failure leaves the invoice as Draft.
        invoice.Send();

        // Render the user's message body through the template service so
        // {InvoiceDetails} and line breaks become proper HTML.
        var html = emailTemplateService.GenerateInvoiceEmailHtml(
            invoice, total, companyName, request.Body, publicViewUrl);

        // Generate the invoice PDF and attach it to the email.
        var attachments = new List<EmailAttachment>();
        var vndRate = await VndPaymentRate.GetAsync(exchangeRates, businessProfile?.DefaultCurrency, cancellationToken);
        var pdfBytes = await pdfService.GenerateInvoicePdfAsync(
            invoice, timeEntries, expenses, productLineItems, businessProfile, currencySettings, invoice.Template, vndRate);
        if (pdfBytes.Length > 0)
        {
            attachments.Add(new EmailAttachment($"{invoice.InvoiceNumber}.pdf", pdfBytes, "application/pdf"));
        }

        await emailSender.SendEmailAsync(request.Email, request.Subject, html, attachments);

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
