using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.RecurringExpenses;

namespace Akinai.Shared.Api.RecurringExpenses;

[Route("api/recurring-expenses")]
public class GetRecurringExpenses : IGet<PaginatedResponse<RecurringExpenseSummaryDto>>, IPaginatedRequest
{
    [QueryStringParam]
    public int Page { get; set; } = 1;

    [QueryStringParam]
    public int PageSize { get; set; } = 15;

    [QueryStringParam]
    public string? Search { get; set; }

    [QueryStringParam]
    public string? Category { get; init; }

    [QueryStringParam]
    public bool? IsActive { get; init; }

    [QueryStringParam]
    public Guid? ClientId { get; init; }

    [QueryStringParam]
    public Guid? ProjectId { get; init; }
}
