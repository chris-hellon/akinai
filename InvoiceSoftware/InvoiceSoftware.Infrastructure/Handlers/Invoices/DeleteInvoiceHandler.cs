using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Shared.Api.Invoices;
using InvoiceSoftware.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Infrastructure.Handlers.Invoices;

public class DeleteInvoiceHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService) : IHandle<DeleteInvoice>
{
    public async Task<HttpResult> Handle(DeleteInvoice request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult.Unauthorized();

        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == request.Id && i.Client.UserId == userId, cancellationToken);
        if (invoice is null) return HttpResult.NotFound();

        var entries = await db.TimeEntries.Where(e => e.InvoiceId == request.Id).ToListAsync(cancellationToken);
        foreach (var entry in entries)
        {
            entry.UnlinkFromInvoice();
        }

        db.Invoices.Remove(invoice);
        await db.SaveChangesAsync(cancellationToken);
        return HttpResult.NoContent();
    }
}
