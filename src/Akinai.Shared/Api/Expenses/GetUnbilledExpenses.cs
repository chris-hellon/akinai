using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.Expenses;

namespace Akinai.Shared.Api.Expenses;

[Route("api/expenses/unbilled")]
public class GetUnbilledExpenses : IGet<List<ExpenseSummaryDto>>
{
    [QueryStringParam]
    public Guid ClientId { get; init; }

    [QueryStringParam]
    public Guid? ProjectId { get; init; }
}
