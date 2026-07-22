using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Shared.Api.Expenses;
using InvoiceSoftware.Shared.Dtos.Expenses;
using InvoiceSoftware.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Infrastructure.Handlers.Expenses;

public class GetUnbilledExpensesHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService) : IHandle<GetUnbilledExpenses, List<ExpenseSummaryDto>>
{
    public async Task<HttpResult<List<ExpenseSummaryDto>>> Handle(GetUnbilledExpenses request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult<List<ExpenseSummaryDto>>.Unauthorized();

        var query = db.Expenses
            .Include(e => e.Client)
            .Include(e => e.Project)
            .Where(e => e.IsBillable && !e.IsBilled && e.ClientId == request.ClientId && e.UserId == userId);

        if (request.ProjectId.HasValue)
            query = query.Where(e => e.ProjectId == request.ProjectId.Value);

        var expenses = await query
            .OrderByDescending(e => e.ExpenseDate)
            .ToListAsync(cancellationToken);

        var result = expenses.Select(e => new ExpenseSummaryDto(
            e.Id,
            e.Category.ToString(),
            e.MerchantName,
            e.ExpenseDate,
            e.PaymentMethod.ToString(),
            e.Amount.Amount,
            e.Amount.Currency,
            e.TaxAmount,
            e.IsBillable,
            e.IsBilled,
            e.ClientId,
            e.Client?.Name,
            e.ProjectId,
            e.Project?.Name,
            e.GroupName,
            e.RecurringExpenseId)).ToList();

        return HttpResult<List<ExpenseSummaryDto>>.Ok(result);
    }
}
