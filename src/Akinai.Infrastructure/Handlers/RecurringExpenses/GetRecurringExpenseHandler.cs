using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.RecurringExpenses;
using Akinai.Shared.Dtos.RecurringExpenses;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.RecurringExpenses;

public class GetRecurringExpenseHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService) : IHandle<GetRecurringExpense, RecurringExpenseDetailDto?>
{
    public async Task<HttpResult<RecurringExpenseDetailDto?>> Handle(GetRecurringExpense request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult<RecurringExpenseDetailDto?>.Unauthorized();

        var expense = await db.RecurringExpenses
            .Include(re => re.Client)
            .Include(re => re.Project)
            .FirstOrDefaultAsync(re => re.Id == request.Id && re.UserId == userId, cancellationToken);

        if (expense is null) return HttpResult<RecurringExpenseDetailDto?>.NotFound();

        var result = new RecurringExpenseDetailDto(
            expense.Id,
            expense.Category.ToString(),
            expense.MerchantName,
            expense.PaymentMethod.ToString(),
            expense.Amount.Amount,
            expense.Amount.Currency,
            expense.TaxAmount,
            expense.IsTaxInclusive,
            expense.MerchantTaxNumber,
            expense.GroupName,
            expense.Notes,
            expense.IsReimbursable,
            expense.IsBillable,
            expense.ClientId,
            expense.Client?.Name,
            expense.ProjectId,
            expense.Project?.Name,
            expense.FrequencyInterval,
            expense.Frequency.ToString(),
            expense.StartDate,
            expense.EndDate,
            expense.IsActive,
            expense.LastGeneratedDate,
            expense.NextExpenseDate,
            expense.GeneratedCount,
            expense.CreatedAt,
            expense.ModifiedAt);

        return HttpResult<RecurringExpenseDetailDto?>.Ok(result);
    }
}
