using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.Enums;
using Akinai.Infrastructure.Data;
using Akinai.Infrastructure.Services;
using Akinai.Shared.Api.BankImport;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.BankImport;

public class MatchTransactionHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService,
    INotificationService notifications)
    : IHandle<MatchTransaction>
{
    public async Task<HttpResult> Handle(MatchTransaction request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var transaction = await db.BankTransactions
            .FirstOrDefaultAsync(bt => bt.Id == request.Id && bt.UserId == userId, cancellationToken);

        if (transaction == null)
            return HttpResult.NotFound();

        // Scoped to the caller like every other invoice lookup. Without the
        // ownership check, knowing an invoice id was enough to mark someone
        // else's invoice paid.
        var invoice = await db.Invoices
            .Include(i => i.Client)
            .FirstOrDefaultAsync(i => i.Id == request.InvoiceId && i.Client.UserId == userId, cancellationToken);

        if (invoice == null)
            return HttpResult.NotFound();

        try
        {
            transaction.MatchToInvoice(request.InvoiceId, MatchConfidence.High, "Manually matched");

            // Only mark paid when it isn't already settled/void — linking a payment to an
            // already-paid invoice should just record the link, not throw.
            var settled = request.MarkInvoiceAsPaid &&
                invoice.Status != InvoiceStatus.Paid &&
                invoice.Status != InvoiceStatus.Void;

            if (settled)
                invoice.MarkAsPaid(DateOnly.FromDateTime(DateTime.UtcNow));

            await db.SaveChangesAsync(cancellationToken);

            // Raised only when this call actually settled the invoice, so
            // re-matching a transaction to an already-paid invoice doesn't
            // announce a second payment that never happened.
            if (settled)
            {
                await notifications.CreateAsync(
                    userId,
                    NotificationType.PaymentReceived,
                    "Payment received",
                    $"{invoice.Client.Name} paid {invoice.InvoiceNumber} — matched from your bank import.",
                    link: $"/invoices/{invoice.Id}",
                    relatedEntityType: LinkedEntityType.Invoice,
                    relatedEntityId: invoice.Id,
                    cancellationToken: cancellationToken);
            }

            return HttpResult.Ok();
        }
        catch (Domain.Exceptions.DomainException)
        {
            return HttpResult.NotFound();
        }
    }
}
