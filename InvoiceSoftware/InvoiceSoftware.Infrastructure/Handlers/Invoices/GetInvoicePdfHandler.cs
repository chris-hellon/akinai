using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Infrastructure.Services;
using InvoiceSoftware.Shared.Api.Invoices;
using InvoiceSoftware.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Infrastructure.Handlers.Invoices;

public class GetInvoicePdfHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService,
    PdfGeneratorService pdfService,
    IExchangeRateService exchangeRates,
    IInvoiceTranslator translator,
    Services.Pdf.IHtmlPdfRenderer htmlRenderer,
    IHttpContextAccessor httpContextAccessor)
    : IHandle<GetInvoicePdf, byte[]>
{
    public async Task<HttpResult<byte[]>> Handle(GetInvoicePdf request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var invoice = await db.Invoices
            .Include(i => i.Client)
            .Include(i => i.Template)
            .FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken);

        if (invoice == null)
            return HttpResult<byte[]>.NotFound();

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

        if (timeEntries.Count == 0 && expenses.Count == 0 && productLineItems.Count == 0)
            return HttpResult<byte[]>.NotFound();

        var userId = currentUserService.UserId;
        Domain.Entities.BusinessProfile? businessProfile = null;
        Domain.Entities.CurrencyPaymentSettings? currencySettings = null;
        if (!string.IsNullOrEmpty(userId))
        {
            businessProfile = await db.BusinessProfiles.FirstOrDefaultAsync(b => b.UserId == userId, cancellationToken);
            currencySettings = await db.CurrencyPaymentSettings
                .FirstOrDefaultAsync(c => c.UserId == userId && c.CurrencyCode == invoice.Currency, cancellationToken);
        }

        var vndRate = await VndPaymentRate.GetAsync(exchangeRates, businessProfile?.DefaultCurrency, cancellationToken);

        // Same language and the same translated text the client sees on the web,
        // so the emailed PDF and the public link cannot say different things.
        var language = await translator.ResolveForInvoiceAsync(
            invoice.CreatedBy, invoice.ClientId, ToShared(invoice.Language), cancellationToken);

        IReadOnlyDictionary<string, string> translations = new Dictionary<string, string>();
        if (language != Shared.Enums.SupportedLanguage.English)
        {
            // Keyed by the source text itself: the generator reads entities
            // directly, so it looks translations up by what it already has.
            var texts = new Dictionary<string, string>();
            foreach (var name in timeEntries.Select(e => e.Job.Name))
                texts[name] = name;
            foreach (var li in productLineItems.Where(li => !string.IsNullOrWhiteSpace(li.Description)))
                texts[li.Description] = li.Description;
            if (!string.IsNullOrWhiteSpace(invoice.Notes))
                texts[invoice.Notes] = invoice.Notes;

            if (texts.Count > 0)
                translations = await translator.TranslateAsync(
                    invoice.CreatedBy, language, texts, cancellationToken);
        }

        // --- Print the page, don't rebuild it -----------------------------
        // The invoice already exists as HTML at /invoice/print/{token}: same
        // component, same data, same translation as the link the client opens.
        // Printing that is what keeps the two from disagreeing.
        if (invoice.PublicAccessToken is Guid token)
        {
            var request2 = httpContextAccessor.HttpContext?.Request;
            if (request2 is not null)
            {
                // Loopback, so the render never leaves the machine and no
                // reverse proxy or auth layer sits in the way.
                var origin = $"{request2.Scheme}://127.0.0.1:{request2.Host.Port ?? (request2.IsHttps ? 443 : 80)}";
                var rendered = await htmlRenderer.RenderAsync($"{origin}/invoice/print/{token}", cancellationToken);
                if (rendered is { Length: > 0 })
                    return HttpResult<byte[]>.Ok(rendered);
            }
        }

        // Fallback: the original QuestPDF generator. Kept because a missing or
        // un-launchable Chromium must not stop someone downloading an invoice —
        // an English-only PDF beats an error.
        var pdfBytes = await pdfService.GenerateInvoicePdfAsync(
            invoice, timeEntries, expenses, productLineItems, businessProfile, currencySettings,
            invoice.Template, vndRate, language.ToString(), translations);

        return pdfBytes.Length == 0 ? HttpResult<byte[]>.NotFound() : HttpResult<byte[]>.Ok(pdfBytes);
    }

    /// <summary>Domain enum to shared enum; both declare the same members in the same order.</summary>
    private static Shared.Enums.SupportedLanguage? ToShared(Domain.Enums.SupportedLanguage? language)
        => language is null ? null : (Shared.Enums.SupportedLanguage)(int)language.Value;
}
