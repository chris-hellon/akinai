using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Shared.Api.Invoices;
using InvoiceSoftware.Shared.Dtos.CurrencyPaymentSettings;
using InvoiceSoftware.Shared.Dtos.Invoices;
using InvoiceSoftware.Shared.Dtos.InvoiceTemplates;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Infrastructure.Handlers.Invoices;

/// <summary>
/// Handler for public invoice access - no authentication required.
/// </summary>
public class GetPublicInvoiceHandler(IDbContextFactory<ApplicationDbContext> dbFactory,
    Services.IExchangeRateService exchangeRates)
    : IHandle<GetPublicInvoice, PublicInvoiceDto?>
{
    public async Task<HttpResult<PublicInvoiceDto?>> Handle(GetPublicInvoice request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // Find invoice by public access token
        var invoice = await db.Invoices
            .Include(i => i.Client)
            .Include(i => i.Template)
            .FirstOrDefaultAsync(i => i.PublicAccessToken == request.Token, cancellationToken);

        if (invoice == null)
            return HttpResult<PublicInvoiceDto?>.NotFound();

        // Get time entries for this invoice
        var timeEntries = await db.TimeEntries
            .Include(e => e.Job)
                .ThenInclude(j => j.Project)
                    .ThenInclude(p => p.Client)
            .Include(e => e.Job)
                .ThenInclude(j => j.Section)
            .Where(e => e.InvoiceId == invoice.Id)
            .ToListAsync(cancellationToken);

        // Get expenses for this invoice
        var expenses = await db.Expenses
            .Include(e => e.Project)
            .Where(e => e.InvoiceId == invoice.Id)
            .ToListAsync(cancellationToken);

        // Get product line items for this invoice
        var productLineItems = await db.InvoiceLineItems
            .Include(li => li.Product)
            .Where(li => li.InvoiceId == invoice.Id)
            .OrderBy(li => li.Order)
            .ToListAsync(cancellationToken);

        // Get business profile (owner of the invoice)
        var businessProfile = await db.BusinessProfiles
            .FirstOrDefaultAsync(b => b.UserId == invoice.CreatedBy, cancellationToken);

        // Get currency-specific payment settings
        var currencySettings = await db.CurrencyPaymentSettings
            .FirstOrDefaultAsync(c => c.UserId == invoice.CreatedBy && c.CurrencyCode == invoice.Currency, cancellationToken);

        // Calculate totals (same as in GetInvoiceHandler)
        var timeEntriesSubtotal = timeEntries.Sum(e => e.BillableHours * e.Job.GetEffectiveHourlyRate());
        var expensesSubtotal = expenses.Sum(e => e.GetTotalAmount());
        var productsSubtotal = productLineItems.Sum(p => p.LineTotal.Amount);
        var subtotal = timeEntriesSubtotal + expensesSubtotal + productsSubtotal;
        var taxAmount = subtotal * (invoice.TaxRate / 100);
        var total = subtotal + taxAmount;

        // Build line items
        var lineItems = timeEntries.Select(e =>
        {
            var projectName = e.Job.Project.Name;
            var description = e.Description ?? e.Job.Name;
            var billedHours = e.BillableHours;
            var rate = e.Job.GetEffectiveHourlyRate();

            return new InvoiceLineItemDto(
                e.Id,
                e.JobId,
                e.Job.Name,
                projectName,
                e.Job.Section?.Name,
                e.Date,
                $"{projectName}: {description}",
                billedHours,
                rate,
                billedHours * rate,
                invoice.Currency);
        }).ToList();

        // Build expense line items
        var expenseLineItems = expenses.Select(e => new InvoiceExpenseLineItemDto(
            e.Id,
            e.Category.ToString(),
            e.MerchantName,
            e.ExpenseDate,
            e.Notes ?? e.MerchantName,
            e.Amount.Amount,
            e.TaxAmount,
            e.GetTotalAmount(),
            e.Amount.Currency,
            e.Project?.Name)).ToList();

        // Build product line items
        var productLineItemDtos = productLineItems.Select(p => new InvoiceProductLineItemDto(
            p.Id,
            p.ProductId,
            p.Product?.Name,
            p.Description,
            p.Quantity,
            p.UnitPrice.Amount,
            p.LineTotal.Amount,
            p.UnitPrice.Currency,
            p.Order)).ToList();

        // Build business address string
        string? businessAddress = null;
        if (businessProfile?.Address != null)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(businessProfile.Address.Street1))
                parts.Add(businessProfile.Address.Street1);
            if (!string.IsNullOrEmpty(businessProfile.Address.Street2))
                parts.Add(businessProfile.Address.Street2);
            var cityLine = string.Join(", ",
                new[] { businessProfile.Address.City, businessProfile.Address.State, businessProfile.Address.PostalCode }
                    .Where(s => !string.IsNullOrEmpty(s)));
            if (!string.IsNullOrEmpty(cityLine))
                parts.Add(cityLine);
            if (!string.IsNullOrEmpty(businessProfile.Address.Country))
                parts.Add(businessProfile.Address.Country);
            businessAddress = string.Join("\n", parts);
        }

        // Build client address string
        string? clientAddress = null;
        if (invoice.Client.BillingAddress != null)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(invoice.Client.BillingAddress.Street1))
                parts.Add(invoice.Client.BillingAddress.Street1);
            if (!string.IsNullOrEmpty(invoice.Client.BillingAddress.Street2))
                parts.Add(invoice.Client.BillingAddress.Street2);
            var cityLine = string.Join(", ",
                new[] { invoice.Client.BillingAddress.City, invoice.Client.BillingAddress.State, invoice.Client.BillingAddress.PostalCode }
                    .Where(s => !string.IsNullOrEmpty(s)));
            if (!string.IsNullOrEmpty(cityLine))
                parts.Add(cityLine);
            if (!string.IsNullOrEmpty(invoice.Client.BillingAddress.Country))
                parts.Add(invoice.Client.BillingAddress.Country);
            clientAddress = string.Join("\n", parts);
        }

        // Build currency payment settings DTO
        CurrencyPaymentSettingsDto? currencyPaymentDto = null;
        if (currencySettings != null)
        {
            currencyPaymentDto = new CurrencyPaymentSettingsDto(
                currencySettings.Id,
                currencySettings.CurrencyCode,
                currencySettings.BankName,
                currencySettings.BankAccountName,
                currencySettings.BankAccountNumber,
                currencySettings.BankSortCode,
                currencySettings.BankIban,
                currencySettings.BankSwift,
                currencySettings.VietQrBankCode);
        }

        // Map template to DTO
        InvoiceTemplateDto? templateDto = null;
        if (invoice.Template != null)
        {
            templateDto = new InvoiceTemplateDto(
                invoice.Template.Id,
                invoice.Template.Name,
                invoice.Template.Description,
                invoice.Template.IsDefault,
                invoice.Template.IsSystem,
                invoice.Template.TemplateType.ToString(),
                invoice.Template.PrimaryColor,
                invoice.Template.AccentColor,
                invoice.Template.TextColor,
                invoice.Template.BackgroundColor,
                invoice.Template.ShowLogo,
                invoice.Template.ShowPaymentQR,
                invoice.Template.ShowBankDetails,
                invoice.Template.ShowItemDescriptions,
                invoice.Template.HeaderLayout.ToString(),
                invoice.Template.ItemsLayout.ToString(),
                invoice.Template.FooterLayout.ToString(),
                invoice.Template.FontFamily,
                invoice.Template.CustomCss);
        }

        var result = new PublicInvoiceDto(
            invoice.Id,
            invoice.InvoiceNumber,
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
            // Business info
            businessProfile?.TradingName ?? businessProfile?.CompanyName,
            businessAddress,
            businessProfile?.Email?.Value,
            businessProfile?.Phone?.Value,
            businessProfile?.Logo,
            businessProfile?.TaxNumber,
            businessProfile?.RegistrationNumber,
            // Payment options
            businessProfile?.PayPalMeUsername,
            businessProfile?.WiseEmail,
            businessProfile?.RevolutUsername,
            // VND conversion settings
            businessProfile?.DefaultCurrency,
            await Services.VndPaymentRate.GetAsync(exchangeRates, businessProfile?.DefaultCurrency, cancellationToken),
            currencyPaymentDto,
            templateDto);

        return HttpResult<PublicInvoiceDto?>.Ok(result);
    }

}
