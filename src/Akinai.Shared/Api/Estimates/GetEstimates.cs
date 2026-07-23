using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.Estimates;

namespace Akinai.Shared.Api.Estimates;

[Route("api/estimates")]
public class GetEstimates : IGet<PaginatedResponse<EstimateSummaryDto>>, IPaginatedRequest
{
    [QueryStringParam]
    public int Page { get; set; } = 1;

    [QueryStringParam]
    public int PageSize { get; set; } = 15;

    [QueryStringParam]
    public string? Search { get; set; }

    [QueryStringParam]
    public string? Status { get; init; }

    [QueryStringParam]
    public Guid? ClientId { get; init; }
}
