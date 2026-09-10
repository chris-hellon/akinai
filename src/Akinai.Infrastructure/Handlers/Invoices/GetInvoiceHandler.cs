using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.Entities;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Invoices;
using Akinai.Shared.Dtos.Invoices;
using Akinai.Shared.Dtos.InvoiceTemplates;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

using Expense = Akinai.Domain.Entities.Expense;

namespace Akinai.Infrastructure.Handlers.Invoices;

public class GetInvoiceHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService,
    IInvoiceTranslator translator) : IHandle<GetInvoice, InvoiceDetailDto?>
{
    public async Task<HttpResult<InvoiceDetailDto?>> Handle(GetInvoice request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult<InvoiceDetailDto?>.Unauthorized();

        var invoice = await db.Invoices
            .Include(i => i.Client)
            .Include(i => i.Template)
            .Include(i => i.ServiceLines)
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

        // Totals come from the snapshot, not from recomputing the rows below, so the
        // detail view agrees with the list, the dashboard and the PDF the client got.
        // The entries are still loaded because the line items are built from them.
        var subtotal = invoice.Subtotal;
        var taxAmount = invoice.TaxAmount;
        var total = invoice.Total;

        var clientAddress = invoice.Client.BillingAddress != null
            ? $"{invoice.Client.BillingAddress.Street1}, {invoice.Client.BillingAddress.City}, {invoice.Client.BillingAddress.State} {invoice.Client.BillingAddress.PostalCode}"
            : null;

        // A saved summary replaces the per-entry rows entirely. The entries are
        // still loaded and still linked — reporting needs them — they simply
        // stop being what anyone reads.
        var lineItems = invoice.ServiceLines.Count > 0
            ? ServiceLineProjection.ToLineItems(invoice.ServiceLines, invoice.Currency)
            : timeEntries.Select(e =>
        {
            var jobName = e.Job.Name;
            var projectName = e.Job.Project.Name;
            var sectionName = e.Job.Section?.Name;
            var description = e.Description ?? jobName;
            if (!string.IsNullOrEmpty(projectName))
            {
                description = $"{projectName}: {description}";
            }
            var hourlyRate = e.BillingRate;
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

        // The linked template, or the account's default when none is linked —
        // so an invoice with no template of its own still honours the default
        // the user chose (centred header, logo, colours).
        var template = await EffectiveTemplate.ResolveAsync(db, userId, invoice.Template, cancellationToken);

        // Map template to DTO if present
        InvoiceTemplateDto? templateDto = null;
        if (template != null)
        {
            var t = template;
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

        // The language this invoice goes out in. Always resolved, so the
        // read-only view can label itself the way the client will see it.
        var language = await translator.ResolveForInvoiceAsync(userId, invoice.ClientId, ToShared(invoice.Language), cancellationToken);
        var notes = invoice.Notes;

        // Freeform text is only translated when the caller asked. The edit form
        // loads through this same handler and must receive the original text.
        if (request.Translated && language != Shared.Enums.SupportedLanguage.English)
        {
            var texts = InvoiceTranslationFields.Collect(
                lineItems, expenseLineItems, productLineItemDtos, notes);

            var translated = await translator.TranslateAsync(userId, language, texts, cancellationToken);

            InvoiceTranslationFields.Apply(
                translated, lineItems, expenseLineItems, productLineItemDtos, ref notes);
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
            notes,
            lineItems,
            expenseLineItems,
            productLineItemDtos,
            invoice.PublicAccessToken,
            invoice.TemplateId,
            templateDto,
            language.ToString(),
            invoice.Language?.ToString(),
            invoice.FirstViewedAtUtc,
            invoice.LastViewedAtUtc,
            invoice.ViewCount,
            invoice.HasSummarisedServices,
            timeEntries.Count);

        return HttpResult<InvoiceDetailDto?>.Ok(result);
    }

    /// <summary>Domain enum to shared enum; both declare the same members in the same order.</summary>
    private static Shared.Enums.SupportedLanguage? ToShared(Domain.Enums.SupportedLanguage? language)
        => language is null ? null : (Shared.Enums.SupportedLanguage)(int)language.Value;
}
