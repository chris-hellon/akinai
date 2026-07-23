using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.RecurringExpenses;

[Route("api/recurring-expenses/{Id}/pause")]
public class PauseRecurringExpense : IPost
{
    [RouteParam]
    public Guid Id { get; init; }
}
