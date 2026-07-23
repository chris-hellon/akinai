using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.RecurringInvoices;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.RecurringInvoices;

public class DeleteRecurringInvoiceHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService)
    : IHandle<DeleteRecurringInvoice>
{
    public async Task<HttpResult> Handle(DeleteRecurringInvoice request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var recurring = await db.RecurringInvoices
            .Include(r => r.LineItems)
            .FirstOrDefaultAsync(r => r.Id == request.Id && r.UserId == userId, cancellationToken);

        if (recurring == null)
            return HttpResult.NotFound();

        db.RecurringInvoiceLineItems.RemoveRange(recurring.LineItems);
        db.RecurringInvoices.Remove(recurring);
        await db.SaveChangesAsync(cancellationToken);
        return HttpResult.NoContent();
    }
}
