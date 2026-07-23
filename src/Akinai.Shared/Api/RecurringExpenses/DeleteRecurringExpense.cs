using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.RecurringExpenses;

[Route("api/recurring-expenses/{Id}")]
public class DeleteRecurringExpense : IDelete
{
    [RouteParam]
    public Guid Id { get; init; }
}
