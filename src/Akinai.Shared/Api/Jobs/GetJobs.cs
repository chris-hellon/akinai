using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.Jobs;

namespace Akinai.Shared.Api.Jobs;

[Route("api/jobs")]
public class GetJobs : IGet<PaginatedResponse<JobDto>>, IPaginatedRequest
{
    [QueryStringParam]
    public int Page { get; set; } = 1;

    [QueryStringParam]
    public int PageSize { get; set; } = 15;

    [QueryStringParam]
    public string? Search { get; set; }

    [QueryStringParam]
    public Guid? ProjectId { get; init; }

    [QueryStringParam]
    public string? Status { get; init; }
}
