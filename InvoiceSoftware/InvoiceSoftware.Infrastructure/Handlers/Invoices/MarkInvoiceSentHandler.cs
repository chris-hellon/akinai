using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Domain.Enums;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Infrastructure.Services;
using InvoiceSoftware.Shared.Api.Invoices;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Infrastructure.Handlers.Invoices;

public class MarkInvoiceSentHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    INotificationService notifications) : IHandle<MarkInvoiceSent>
{
    public async Task<HttpResult> Handle(MarkInvoiceSent request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var invoice = await db.Invoices
            .Include(i => i.Client)
            .FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken);
        if (invoice is null) return HttpResult.NotFound();

        if (invoice.Status != InvoiceStatus.Draft)
            return HttpResult.NoContent(); // already sent/paid/etc. — nothing to do

        invoice.Send();
        await db.SaveChangesAsync(cancellationToken);

        await notifications.CreateAsync(
            invoice.Client?.UserId,
            NotificationType.InvoiceSent,
            "Invoice sent",
            $"{invoice.InvoiceNumber} marked as sent to {invoice.Client?.Name}",
            link: $"/invoices/{invoice.Id}",
            relatedEntityType: LinkedEntityType.Invoice,
            relatedEntityId: invoice.Id,
            cancellationToken: cancellationToken);

        return HttpResult.NoContent();
    }
}
