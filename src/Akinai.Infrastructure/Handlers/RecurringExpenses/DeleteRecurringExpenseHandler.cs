using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.RecurringExpenses;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.RecurringExpenses;

public class DeleteRecurringExpenseHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService) : IHandle<DeleteRecurringExpense>
{
    public async Task<HttpResult> Handle(DeleteRecurringExpense request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult.Unauthorized();

        var recurringExpense = await db.RecurringExpenses
            .Include(re => re.GeneratedExpenses)
            .FirstOrDefaultAsync(re => re.Id == request.Id && re.UserId == userId, cancellationToken);

        if (recurringExpense is null)
            return HttpResult.NotFound();

        // Unlink generated expenses (set RecurringExpenseId to null)
        var generatedExpenses = await db.Expenses
            .Where(e => e.RecurringExpenseId == request.Id)
            .ToListAsync(cancellationToken);

        // The FK has ON DELETE SET NULL so this will be handled automatically

        db.RecurringExpenses.Remove(recurringExpense);
        await db.SaveChangesAsync(cancellationToken);

        return HttpResult.NoContent();
    }
}
