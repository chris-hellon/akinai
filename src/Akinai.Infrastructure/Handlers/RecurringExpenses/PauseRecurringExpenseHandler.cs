using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.RecurringExpenses;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.RecurringExpenses;

public class PauseRecurringExpenseHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService) : IHandle<PauseRecurringExpense>
{
    public async Task<HttpResult> Handle(PauseRecurringExpense request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult.Unauthorized();

        var recurringExpense = await db.RecurringExpenses.FirstOrDefaultAsync(re => re.Id == request.Id && re.UserId == userId, cancellationToken);
        if (recurringExpense is null) return HttpResult.NotFound();

        recurringExpense.Pause();

        await db.SaveChangesAsync(cancellationToken);
        return HttpResult.NoContent();
    }
}
