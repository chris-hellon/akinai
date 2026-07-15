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

public class GetInvoiceEmailTemplateHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService,
    EmailTemplateService emailTemplateService,
    IHttpContextAccessor httpContextAccessor)
    : IHandle<GetInvoiceEmailTemplate, string?>
{
    public async Task<HttpResult<string?>> Handle(GetInvoiceEmailTemplate request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var invoice = await db.Invoices
            .Include(i => i.Client)
            .FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken);

        if (invoice is null)
            return HttpResult<string?>.NotFound();

        var timeEntries = await db.TimeEntries
            .Include(e => e.Job)
                .ThenInclude(j => j.Project)
                    .ThenInclude(p => p.Client)
            .Where(e => e.InvoiceId == request.Id)
            .ToListAsync(cancellationToken);

        var expenses = await db.Expenses
            .Where(e => e.InvoiceId == request.Id)
            .ToListAsync(cancellationToken);

        var total = CalculateInvoiceTotal(timeEntries, expenses, invoice.TaxRate);

        // Get company name and email template from business profile
        var userId = currentUserService.UserId;
        var companyName = "Your Company";
        string? emailBodyTemplate = null;

        if (!string.IsNullOrEmpty(userId))
        {
            var profile = await db.BusinessProfiles
                .FirstOrDefaultAsync(b => b.UserId == userId, cancellationToken);
            if (profile != null)
            {
                companyName = profile.TradingName ?? profile.CompanyName ?? companyName;
                emailBodyTemplate = profile.InvoiceEmailBody;
            }
        }

        // Build public view URL if token exists
        string? publicViewUrl = null;
        if (invoice.PublicAccessToken.HasValue && httpContextAccessor.HttpContext != null)
        {
            var request2 = httpContextAccessor.HttpContext.Request;
            var baseUrl = $"{request2.Scheme}://{request2.Host}";
            publicViewUrl = $"{baseUrl}/invoice/view/{invoice.PublicAccessToken}";
        }

        var html = emailTemplateService.GenerateInvoiceEmailHtml(invoice, total, companyName, emailBodyTemplate, publicViewUrl);

        return HttpResult<string?>.Ok(html);
    }

    private static decimal CalculateInvoiceTotal(List<TimeEntry> timeEntries, List<Expense> expenses, decimal taxRate)
    {
        var timeSubtotal = timeEntries.Sum(e => e.BillableHours * e.Job.GetEffectiveHourlyRate());
        var expenseSubtotal = expenses.Sum(e => e.GetTotalAmount());
        var subtotal = timeSubtotal + expenseSubtotal;
        var taxAmount = subtotal * (taxRate / 100);
        return subtotal + taxAmount;
    }

}
