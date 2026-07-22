using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Shared.Api.Expenses;
using InvoiceSoftware.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Infrastructure.Handlers.Expenses;

public class DeleteExpenseHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService) : IHandle<DeleteExpense>
{
    public async Task<HttpResult> Handle(DeleteExpense request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult.Unauthorized();

        var expense = await db.Expenses.FirstOrDefaultAsync(e => e.Id == request.Id && e.UserId == userId, cancellationToken);

        if (expense is null)
            return HttpResult.NotFound();

        // Release any imported bank transaction linked to this expense so it doesn't
        // point at a now-deleted record — it returns to "Needs review".
        var linkedTransactions = await db.BankTransactions
            .Where(bt => bt.ExpenseId == request.Id)
            .ToListAsync(cancellationToken);
        foreach (var transaction in linkedTransactions)
            transaction.UnlinkExpense();

        db.Expenses.Remove(expense);
        await db.SaveChangesAsync(cancellationToken);

        return HttpResult.NoContent();
    }
}
