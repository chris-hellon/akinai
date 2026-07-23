using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.RecurringInvoices;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.RecurringInvoices;

public class GenerateInvoiceNowHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService)
    : IHandle<GenerateInvoiceNow, Guid?>
{
    public async Task<HttpResult<Guid?>> Handle(GenerateInvoiceNow request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<Guid?>.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var recurring = await db.RecurringInvoices
            .Include(r => r.LineItems)
            .FirstOrDefaultAsync(r => r.Id == request.Id && r.UserId == userId, cancellationToken);

        if (recurring == null)
            return HttpResult<Guid?>.NotFound();

        if (!recurring.LineItems.Any())
            return HttpResult<Guid?>.NotFound();

        // Generate invoice number
        var lastInvoice = await db.Invoices.OrderByDescending(i => i.InvoiceNumber).FirstOrDefaultAsync(cancellationToken);
        var nextNumber = 1;
        if (lastInvoice != null && lastInvoice.InvoiceNumber.StartsWith("INV-"))
        {
            if (int.TryParse(lastInvoice.InvoiceNumber[4..], out var num))
                nextNumber = num + 1;
        }
        var invoiceNumber = $"INV-{nextNumber:D5}";

        // Force generation for current date
        var asOfDate = DateOnly.FromDateTime(DateTime.UtcNow);

        // Temporarily set NextInvoiceDate to allow generation
        var originalNextDate = recurring.NextInvoiceDate;
        var wasActive = recurring.IsActive;

        // We'll call GenerateInvoice with a date far in the future to force generation
        var invoice = recurring.GenerateInvoice(invoiceNumber, asOfDate.AddYears(100));

        if (invoice == null)
        {
            // If generation failed, it might be because the recurring invoice is paused
            return HttpResult<Guid?>.NotFound();
        }

        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(cancellationToken);
        return HttpResult<Guid?>.Ok(invoice.Id);
    }
}
