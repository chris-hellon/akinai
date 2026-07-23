using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.Expenses;

namespace Akinai.Shared.Api.Expenses;

[Route("api/expenses/{Id}")]
public class GetExpense : IGet<ExpenseDetailDto?>
{
    [RouteParam]
    public Guid Id { get; init; }
}
