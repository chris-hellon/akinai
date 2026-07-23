using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.Enums;
using Akinai.Infrastructure.Data;
using Akinai.Infrastructure.Services;
using Akinai.Shared.Api.Invoices;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Invoices;

public class MarkInvoiceSentHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService,
    INotificationService notifications) : IHandle<MarkInvoiceSent>
{
    public async Task<HttpResult> Handle(MarkInvoiceSent request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult.Unauthorized();

        var invoice = await db.Invoices
            .Include(i => i.Client)
            .FirstOrDefaultAsync(i => i.Id == request.Id && i.Client.UserId == userId, cancellationToken);
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
