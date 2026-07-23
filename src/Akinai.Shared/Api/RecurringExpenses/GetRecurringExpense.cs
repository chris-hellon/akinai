using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.RecurringExpenses;

namespace Akinai.Shared.Api.RecurringExpenses;

[Route("api/recurring-expenses/{Id}")]
public class GetRecurringExpense : IGet<RecurringExpenseDetailDto?>
{
    [RouteParam]
    public Guid Id { get; init; }
}
