using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.RecurringExpenses;

[Route("api/recurring-expenses/{Id}/resume")]
public class ResumeRecurringExpense : IPost
{
    [RouteParam]
    public Guid Id { get; init; }
}
