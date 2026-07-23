using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.ClientPortal;

namespace Akinai.Shared.Api.ClientPortal;

[Route("api/portal/{ClientToken}/estimates")]
public class GetClientPortalEstimates : IGet<PaginatedResponse<ClientPortalEstimateDto>>, IPaginatedRequest
{
    [RouteParam]
    public Guid ClientToken { get; init; }

    [QueryStringParam]
    public string? Status { get; init; }

    [QueryStringParam]
    public int Page { get; set; } = 1;

    [QueryStringParam]
    public int PageSize { get; set; } = 20;

    [QueryStringParam]
    public string? Search { get; set; }
}
