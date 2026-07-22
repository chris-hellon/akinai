using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Infrastructure.Services;
using InvoiceSoftware.Shared.Api.Invoices;
using InvoiceSoftware.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Infrastructure.Handlers.Invoices;

public class GetInvoicePdfHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService,
    PdfGeneratorService pdfService,
    Services.IExchangeRateService exchangeRates)
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

        var vndRate = await Services.VndPaymentRate.GetAsync(exchangeRates, businessProfile?.DefaultCurrency, cancellationToken);
        var pdfBytes = await pdfService.GenerateInvoicePdfAsync(invoice, timeEntries, expenses, productLineItems, businessProfile, currencySettings, invoice.Template, vndRate);

        return pdfBytes.Length == 0 ? HttpResult<byte[]>.NotFound() : HttpResult<byte[]>.Ok(pdfBytes);
    }
}
