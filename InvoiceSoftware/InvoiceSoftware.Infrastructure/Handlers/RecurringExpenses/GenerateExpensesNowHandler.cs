using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Shared.Api.RecurringExpenses;
using InvoiceSoftware.Shared.Dtos.Expenses;
using InvoiceSoftware.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Infrastructure.Handlers.RecurringExpenses;

public class GenerateExpensesNowHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService) : IHandle<GenerateExpensesNow, ExpenseSummaryDto?>
{
    public async Task<HttpResult<ExpenseSummaryDto?>> Handle(GenerateExpensesNow request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult<ExpenseSummaryDto?>.Unauthorized();

        var recurringExpense = await db.RecurringExpenses
            .Include(re => re.Client)
            .Include(re => re.Project)
            .FirstOrDefaultAsync(re => re.Id == request.Id && re.UserId == userId, cancellationToken);

        if (recurringExpense is null) return HttpResult<ExpenseSummaryDto?>.NotFound();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var expense = recurringExpense.GenerateExpense(today);

        if (expense is null)
            return HttpResult<ExpenseSummaryDto?>.Ok(null);

        db.Expenses.Add(expense);
        await db.SaveChangesAsync(cancellationToken);

        var result = new ExpenseSummaryDto(
            expense.Id,
            expense.Category.ToString(),
            expense.MerchantName,
            expense.ExpenseDate,
            expense.PaymentMethod.ToString(),
            expense.Amount.Amount,
            expense.Amount.Currency,
            expense.TaxAmount,
            expense.IsBillable,
            expense.IsBilled,
            expense.ClientId,
            recurringExpense.Client?.Name,
            expense.ProjectId,
            recurringExpense.Project?.Name,
            expense.GroupName,
            expense.RecurringExpenseId);

        return HttpResult<ExpenseSummaryDto?>.Ok(result);
    }
}
