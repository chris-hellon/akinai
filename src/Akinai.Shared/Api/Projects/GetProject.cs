using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.Projects;

namespace Akinai.Shared.Api.Projects;

[Route("api/projects/{Id}")]
public class GetProject : IGet<ProjectDetailDto?>
{
    [RouteParam]
    public Guid Id { get; init; }
}
