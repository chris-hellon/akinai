using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Invoices;
using Akinai.Infrastructure.Handlers.CurrencyPaymentSettings;
using Akinai.Shared.Dtos.CurrencyPaymentSettings;
using Akinai.Shared.Payments;
using Akinai.Shared.Dtos.Invoices;
using Akinai.Shared.Dtos.InvoiceTemplates;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Invoices;

/// <summary>
/// Handler for public invoice access - no authentication required.
/// </summary>
public class GetPublicInvoiceHandler(IDbContextFactory<ApplicationDbContext> dbFactory,
    Services.IExchangeRateService exchangeRates,
    Shared.Services.IInvoiceTranslator translator,
    Shared.Services.ICurrentUserService currentUserService,
    Shared.Services.IEntitlementService entitlements)
    : IHandle<GetPublicInvoice, PublicInvoiceDto?>
{
    public async Task<HttpResult<PublicInvoiceDto?>> Handle(GetPublicInvoice request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // Find invoice by public access token
        var invoice = await db.Invoices
            .Include(i => i.Client)
            .Include(i => i.Template)
            .Include(i => i.ServiceLines)
            .FirstOrDefaultAsync(i => i.PublicAccessToken == request.Token, cancellationToken);

        if (invoice == null)
            return HttpResult<PublicInvoiceDto?>.NotFound();

        // --- View tracking ------------------------------------------------
        // Only when the caller asked (the public web page does, the PDF renderer
        // does not) and only when it is not the owner looking at their own link.
        // Both of those reach this handler identically, and counting either
        // would turn "your client opened this" into a number that means nothing.
        if (request.RecordView && !string.Equals(currentUserService.UserId, invoice.CreatedBy, StringComparison.Ordinal))
        {
            invoice.RecordView(DateTime.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
        }

        // Get time entries for this invoice
        var timeEntries = await db.TimeEntries
            .Include(e => e.Job)
                .ThenInclude(j => j.Project)
                    .ThenInclude(p => p.Client)
            .Include(e => e.Job)
                .ThenInclude(j => j.Section)
            .Where(e => e.InvoiceId == invoice.Id)
            // Chronological, so the services table reads as the work happened —
            // and reads identically in the view, the PDF and the client's copy.
            // CreatedAt breaks ties so same-day entries cannot come back in a
            // different order from one query to the next.
            .OrderBy(e => e.Date)
            .ThenBy(e => e.CreatedAt)
            .ToListAsync(cancellationToken);

        // Get expenses for this invoice
        var expenses = await db.Expenses
            .Include(e => e.Project)
            .Where(e => e.InvoiceId == invoice.Id)
            // Same chronological rule as the services rows above.
            .OrderBy(e => e.ExpenseDate)
            .ThenBy(e => e.CreatedAt)
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

        // Every currency account the owner holds, not just this invoice's own — the client
        // is offered each one this invoice was written to accept, so a cross-border payer
        // can settle on a local rail instead of a four-day correspondent chain.
        var allCurrencySettings = await db.CurrencyPaymentSettings
            .Where(c => c.UserId == invoice.CreatedBy)
            .Select(CurrencyPaymentSettingsMapping.Projection)
            .ToListAsync(cancellationToken);

        var acceptedCurrencies = await db.InvoicePaymentCurrencies
            .Where(pc => pc.InvoiceId == invoice.Id)
            .ToDictionaryAsync(pc => pc.Currency, pc => pc.Amount, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var alternatePaymentOptions = AlternatePaymentCurrencies.Select(
            invoice.Currency, acceptedCurrencies, allCurrencySettings);

        // Totals come from the snapshot (same as in GetInvoiceHandler). This is what the
        // client was billed; recomputing here would let a later rate change silently
        // restate an invoice they have already been sent.
        var subtotal = invoice.Subtotal;
        var taxAmount = invoice.TaxAmount;
        var total = invoice.Total;

        // Build line items. A saved summary replaces the per-entry rows — the
        // entries still drive the subtotal above, they just stop being what the
        // client reads. Translation below then treats a summarised description
        // exactly as it treats a job name.
        var lineItems = invoice.ServiceLines.Count > 0
            ? ServiceLineProjection.ToLineItems(invoice.ServiceLines, invoice.Currency)
            : timeEntries.Select(e =>
        {
            var projectName = e.Job.Project.Name;
            var description = e.Description ?? e.Job.Name;
            var billedHours = e.BillableHours;
            var rate = e.BillingRate;

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

        // --- Translation --------------------------------------------------
        // Done here, on the server, rather than in the browser: this is the
        // response the client actually receives, so the invoice arrives already
        // in their language whether they open the page, print it, or the PDF
        // handler renders the same DTO.
        //
        // Only the freeform text the user wrote goes through the translator.
        // The document's fixed headings are picked from InvoiceLabels by the
        // renderer using the Language field below — no API call, no cost.
        var compliance = await InvoiceCompliance.AssessAsync(
            db, invoice.Id, invoice.CreatedBy, cancellationToken);

        var language = await translator.ResolveForInvoiceAsync(
            invoice.CreatedBy, invoice.ClientId, ToShared(invoice.Language), cancellationToken);

        var notes = invoice.Notes;

        if (language != Shared.Enums.SupportedLanguage.English)
        {
            // Every user-authored field the document actually displays. Note
            // these are not simply "Description": the services table renders the
            // job, project and section names, and the expenses table renders the
            // merchant — translating Description alone left the visible text in
            // English. Expense categories are a closed enum and come from
            // InvoiceLabels instead.
            var texts = InvoiceTranslationFields.Collect(lineItems, expenseLineItems, productLineItemDtos, notes);

            // One call for the whole invoice, deduplicated by content inside the
            // translator. Anything that could not be translated comes back as
            // the original, so this cannot fail the render.
            var translated = await translator.TranslateAsync(
                invoice.CreatedBy, language, texts, cancellationToken);

            InvoiceTranslationFields.Apply(
                translated, lineItems, expenseLineItems, productLineItemDtos, ref notes);
        }

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

        var currencyPaymentDto = allCurrencySettings.FirstOrDefault(
            c => string.Equals(c.CurrencyCode, invoice.Currency, StringComparison.OrdinalIgnoreCase));

        // Linked template, or the owner's default when none is linked, so the
        // client sees the document the owner styled rather than the built-in
        // standard layout.
        var template = await EffectiveTemplate.ResolveAsync(db, invoice.CreatedBy, invoice.Template, cancellationToken);

        // Map template to DTO
        InvoiceTemplateDto? templateDto = null;
        if (template != null)
        {
            templateDto = new InvoiceTemplateDto(
                template.Id,
                template.Name,
                template.Description,
                template.IsDefault,
                template.IsSystem,
                template.TemplateType.ToString(),
                template.PrimaryColor,
                template.AccentColor,
                template.TextColor,
                template.BackgroundColor,
                template.ShowLogo,
                template.ShowPaymentQR,
                template.ShowBankDetails,
                template.ShowItemDescriptions,
                template.HeaderLayout.ToString(),
                template.ItemsLayout.ToString(),
                template.FooterLayout.ToString(),
                template.FontFamily,
                template.CustomCss);
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
            notes,
            lineItems,
            expenseLineItems,
            productLineItemDtos,
            // Business info
            string.IsNullOrWhiteSpace(businessProfile?.TradingName) ? businessProfile?.CompanyName : businessProfile.TradingName,
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
            alternatePaymentOptions,
            templateDto,
            language.ToString(),
            await DocumentBranding.ShowForAsync(entitlements, invoice.CreatedBy, cancellationToken),
            compliance.RequiredStatements.ToList(),
            ShowScanToPay: await ScanToPayEntitlement.ShowForAsync(entitlements, invoice.CreatedBy, cancellationToken));

        return HttpResult<PublicInvoiceDto?>.Ok(result);
    }


    /// <summary>Domain enum to shared enum; both declare the same members in the same order.</summary>
    private static Shared.Enums.SupportedLanguage? ToShared(Domain.Enums.SupportedLanguage? language)
        => language is null ? null : (Shared.Enums.SupportedLanguage)(int)language.Value;
}
