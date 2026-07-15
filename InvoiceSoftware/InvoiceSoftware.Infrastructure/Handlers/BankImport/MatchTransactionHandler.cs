using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Domain.Enums;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Shared.Api.BankImport;
using InvoiceSoftware.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Infrastructure.Handlers.BankImport;

public class MatchTransactionHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService)
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

        var invoice = await db.Invoices
            .FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken);

        if (invoice == null)
            return HttpResult.NotFound();

        try
        {
            transaction.MatchToInvoice(request.InvoiceId, MatchConfidence.High, "Manually matched");

            // Only mark paid when it isn't already settled/void — linking a payment to an
            // already-paid invoice should just record the link, not throw.
            if (request.MarkInvoiceAsPaid &&
                invoice.Status != InvoiceStatus.Paid &&
                invoice.Status != InvoiceStatus.Void)
            {
                invoice.MarkAsPaid(DateOnly.FromDateTime(DateTime.UtcNow));
            }

            await db.SaveChangesAsync(cancellationToken);
            return HttpResult.Ok();
        }
        catch (Domain.Exceptions.DomainException)
        {
            return HttpResult.NotFound();
        }
    }
}
