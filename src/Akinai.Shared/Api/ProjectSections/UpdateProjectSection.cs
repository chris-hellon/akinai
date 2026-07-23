using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.ProjectSections;

[Route("api/projects/sections/{Id}")]
public class UpdateProjectSection : IPut
{
    [RouteParam]
    public Guid Id { get; init; }

    [BodyParam]
    public string Name { get; init; } = null!;

    [BodyParam]
    public string? Description { get; init; }
}
