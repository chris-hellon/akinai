using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Domain.Entities;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Infrastructure.Services;
using InvoiceSoftware.Shared.Api.Invoices;
using InvoiceSoftware.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Infrastructure.Handlers.Invoices;

public class SendInvoiceHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService,
    EmailTemplateService emailTemplateService,
    PdfGeneratorService pdfService,
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
            .ToListAsync(cancellationToken);

        var expenses = await db.Expenses
            .Where(e => e.InvoiceId == request.Id)
            .ToListAsync(cancellationToken);

        var productLineItems = await db.InvoiceLineItems
            .Include(li => li.Product)
            .Where(li => li.InvoiceId == request.Id)
            .OrderBy(li => li.Order)
            .ToListAsync(cancellationToken);

        var total = CalculateInvoiceTotal(timeEntries, expenses, productLineItems, invoice.TaxRate);

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
                companyName = businessProfile.TradingName ?? businessProfile.CompanyName ?? companyName;
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
        var pdfBytes = await pdfService.GenerateInvoicePdfAsync(
            invoice, timeEntries, expenses, productLineItems, businessProfile, currencySettings, invoice.Template);
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

    private static decimal CalculateInvoiceTotal(List<TimeEntry> timeEntries, List<Expense> expenses, List<InvoiceLineItem> productLineItems, decimal taxRate)
    {
        var timeSubtotal = timeEntries.Sum(e => e.BillableHours * e.Job.GetEffectiveHourlyRate());
        var expenseSubtotal = expenses.Sum(e => e.GetTotalAmount());
        var lineItemSubtotal = productLineItems.Sum(li => li.LineTotal.Amount);
        var subtotal = timeSubtotal + expenseSubtotal + lineItemSubtotal;
        var taxAmount = subtotal * (taxRate / 100);
        return subtotal + taxAmount;
    }

}
