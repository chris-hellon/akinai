using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Domain.Entities;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Shared.Api.Invoices;
using InvoiceSoftware.Shared.Dtos.Invoices;
using InvoiceSoftware.Shared.Dtos.InvoiceTemplates;
using InvoiceSoftware.Shared.Services;
using Microsoft.EntityFrameworkCore;

using Expense = InvoiceSoftware.Domain.Entities.Expense;

namespace InvoiceSoftware.Infrastructure.Handlers.Invoices;

public class GetInvoiceHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService) : IHandle<GetInvoice, InvoiceDetailDto?>
{
    public async Task<HttpResult<InvoiceDetailDto?>> Handle(GetInvoice request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult<InvoiceDetailDto?>.Unauthorized();

        var invoice = await db.Invoices
            .Include(i => i.Client)
            .Include(i => i.Template)
            .FirstOrDefaultAsync(i => i.Id == request.Id && i.Client.UserId == userId, cancellationToken);

        if (invoice is null) return HttpResult<InvoiceDetailDto?>.NotFound();

        var timeEntries = await db.TimeEntries
            .Include(e => e.Job)
                .ThenInclude(j => j.Project)
                    .ThenInclude(p => p.Client)
            .Include(e => e.Job)
                .ThenInclude(j => j.Section)
            .Where(e => e.InvoiceId == request.Id)
            .ToListAsync(cancellationToken);

        var expenses = await db.Expenses
            .Include(e => e.Project)
            .Where(e => e.InvoiceId == request.Id)
            .ToListAsync(cancellationToken);

        var productLineItems = await db.InvoiceLineItems
            .Include(li => li.Product)
            .Where(li => li.InvoiceId == request.Id)
            .OrderBy(li => li.Order)
            .ToListAsync(cancellationToken);

        var (subtotal, taxAmount, total) = CalculateInvoiceTotals(timeEntries, expenses, productLineItems, invoice.TaxRate);

        var clientAddress = invoice.Client.BillingAddress != null
            ? $"{invoice.Client.BillingAddress.Street1}, {invoice.Client.BillingAddress.City}, {invoice.Client.BillingAddress.State} {invoice.Client.BillingAddress.PostalCode}"
            : null;

        var lineItems = timeEntries.Select(e =>
        {
            var jobName = e.Job.Name;
            var projectName = e.Job.Project.Name;
            var sectionName = e.Job.Section?.Name;
            var description = e.Description ?? jobName;
            if (!string.IsNullOrEmpty(projectName))
            {
                description = $"{projectName}: {description}";
            }
            var hourlyRate = e.Job.GetEffectiveHourlyRate();
            var billableHours = e.BillableHours;
            var lineTotal = billableHours * hourlyRate;
            var entryCurrency = e.Job.Project.Client.Currency;

            return new InvoiceLineItemDto(
                e.Id,
                e.JobId,
                jobName,
                projectName,
                sectionName,
                e.Date,
                description,
                billableHours,
                hourlyRate,
                lineTotal,
                entryCurrency);
        }).ToList();

        var expenseLineItems = expenses.Select(e =>
        {
            var description = e.Notes ?? e.MerchantName;
            var lineTotal = e.GetTotalAmount();

            return new InvoiceExpenseLineItemDto(
                e.Id,
                e.Category.ToString(),
                e.MerchantName,
                e.ExpenseDate,
                description,
                e.Amount.Amount,
                e.TaxAmount,
                lineTotal,
                e.Amount.Currency,
                e.Project?.Name);
        }).ToList();

        var productLineItemDtos = productLineItems.Select(li => new InvoiceProductLineItemDto(
            li.Id,
            li.ProductId,
            li.Product?.Name,
            li.Description,
            li.Quantity,
            li.UnitPrice.Amount,
            li.LineTotal.Amount,
            li.UnitPrice.Currency,
            li.Order)).ToList();

        // Map template to DTO if present
        InvoiceTemplateDto? templateDto = null;
        if (invoice.Template != null)
        {
            var t = invoice.Template;
            templateDto = new InvoiceTemplateDto(
                t.Id,
                t.Name,
                t.Description,
                t.IsDefault,
                t.IsSystem,
                t.TemplateType.ToString(),
                t.PrimaryColor,
                t.AccentColor,
                t.TextColor,
                t.BackgroundColor,
                t.ShowLogo,
                t.ShowPaymentQR,
                t.ShowBankDetails,
                t.ShowItemDescriptions,
                t.HeaderLayout.ToString(),
                t.ItemsLayout.ToString(),
                t.FooterLayout.ToString(),
                t.FontFamily,
                t.CustomCss);
        }

        var result = new InvoiceDetailDto(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.ClientId,
            invoice.Client.Name,
            invoice.Client.CompanyName,
            invoice.Client.Email.Value,
            clientAddress,
            invoice.Currency,
            invoice.IssueDate,
            invoice.DueDate,
            invoice.PaidDate,
            invoice.Status.ToString(),
            subtotal,
            invoice.TaxRate,
            taxAmount,
            total,
            invoice.Notes,
            lineItems,
            expenseLineItems,
            productLineItemDtos,
            invoice.PublicAccessToken,
            invoice.TemplateId,
            templateDto);

        return HttpResult<InvoiceDetailDto?>.Ok(result);
    }

    private static (decimal Subtotal, decimal TaxAmount, decimal Total) CalculateInvoiceTotals(
        List<TimeEntry> timeEntries, List<Expense> expenses, List<InvoiceLineItem> productLineItems, decimal taxRate)
    {
        var timeSubtotal = timeEntries.Sum(e => e.BillableHours * e.Job.GetEffectiveHourlyRate());
        var expenseSubtotal = expenses.Sum(e => e.GetTotalAmount());
        var productSubtotal = productLineItems.Sum(li => li.LineTotal.Amount);
        var subtotal = timeSubtotal + expenseSubtotal + productSubtotal;
        var taxAmount = subtotal * (taxRate / 100);
        var total = subtotal + taxAmount;
        return (subtotal, taxAmount, total);
    }

}
