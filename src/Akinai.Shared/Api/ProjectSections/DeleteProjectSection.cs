using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.ProjectSections;

[Route("api/projects/sections/{Id}")]
public class DeleteProjectSection : IDelete
{
    [RouteParam]
    public Guid Id { get; init; }
}
