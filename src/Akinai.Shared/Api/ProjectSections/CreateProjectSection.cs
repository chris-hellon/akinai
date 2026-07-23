using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.ProjectSections;

[Route("api/projects/{ProjectId}/sections")]
public class CreateProjectSection : IPost<Guid>
{
    [RouteParam]
    public Guid ProjectId { get; init; }

    [BodyParam]
    public string Name { get; init; } = null!;

    [BodyParam]
    public string? Description { get; init; }
}
