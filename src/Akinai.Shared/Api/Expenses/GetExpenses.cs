using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.Expenses;

namespace Akinai.Shared.Api.Expenses;

[Route("api/expenses")]
public class GetExpenses : IGet<PaginatedResponse<ExpenseSummaryDto>>, IPaginatedRequest
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
    public DateOnly? FromDate { get; init; }

    [QueryStringParam]
    public DateOnly? ToDate { get; init; }

    [QueryStringParam]
    public bool? IsBillable { get; init; }

    [QueryStringParam]
    public bool? IsBilled { get; init; }

    [QueryStringParam]
    public Guid? ClientId { get; init; }

    [QueryStringParam]
    public Guid? ProjectId { get; init; }

    [QueryStringParam]
    public string? GroupName { get; init; }
}
