using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.Projects;

namespace Akinai.Shared.Api.Projects;

[Route("api/projects")]
public class GetProjects : IGet<PaginatedResponse<ProjectDto>>, IPaginatedRequest
{
    [QueryStringParam]
    public int Page { get; set; } = 1;

    [QueryStringParam]
    public int PageSize { get; set; } = 15;

    [QueryStringParam]
    public string? Search { get; set; }

    [QueryStringParam]
    public Guid? ClientId { get; init; }

    [QueryStringParam]
    public string? Status { get; init; }
}
