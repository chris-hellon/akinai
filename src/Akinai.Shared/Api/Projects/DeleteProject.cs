using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.Projects;

[Route("api/projects/{Id}")]
public class DeleteProject : IDelete
{
    [RouteParam]
    public Guid Id { get; init; }
}
