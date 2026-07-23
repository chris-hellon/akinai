using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.ProjectSections;

namespace Akinai.Shared.Api.ProjectSections;

[Route("api/projects/{ProjectId}/sections")]
public class GetProjectSections : IGet<List<ProjectSectionDto>>
{
    [RouteParam]
    public Guid ProjectId { get; init; }
}
