using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.Enums;
using Akinai.Infrastructure.Data;
using Akinai.Infrastructure.Services;
using Akinai.Shared.Api.Invoices;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Invoices;

public class MarkInvoicePaidHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService,
    INotificationService notifications) : IHandle<MarkInvoicePaid>
{
    public async Task<HttpResult> Handle(MarkInvoicePaid request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult.Unauthorized();

        var invoice = await db.Invoices
            .Include(i => i.Client)
            .FirstOrDefaultAsync(i => i.Id == request.Id && i.Client.UserId == userId, cancellationToken);
        if (invoice is null) return HttpResult.NotFound();

        invoice.MarkAsPaid(request.PaidDate);
        db.Entry(invoice).State = EntityState.Modified;
        await db.SaveChangesAsync(cancellationToken);

        await notifications.CreateAsync(
            invoice.Client?.UserId,
            NotificationType.PaymentReceived,
            "Payment received",
            $"{invoice.Client?.Name} paid {invoice.InvoiceNumber}",
            link: $"/invoices/{invoice.Id}",
            relatedEntityType: LinkedEntityType.Invoice,
            relatedEntityId: invoice.Id,
            cancellationToken: cancellationToken);

        return HttpResult.NoContent();
    }
}
