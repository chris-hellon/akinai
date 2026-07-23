using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.Expenses;

namespace Akinai.Shared.Api.RecurringExpenses;

[Route("api/recurring-expenses/{Id}/generate")]
public class GenerateExpensesNow : IPost<ExpenseSummaryDto?>
{
    [RouteParam]
    public Guid Id { get; init; }
}
