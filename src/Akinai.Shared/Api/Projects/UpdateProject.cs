using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.Projects;

[Route("api/projects/{Id}")]
public class UpdateProject : IPut
{
    [RouteParam]
    public Guid Id { get; init; }

    [BodyParam]
    public string Name { get; init; } = null!;

    [BodyParam]
    public string? Description { get; init; }

    [BodyParam]
    public string Status { get; init; } = "Active";

    [BodyParam]
    public DateOnly? StartDate { get; init; }

    [BodyParam]
    public DateOnly? EndDate { get; init; }

    [BodyParam]
    public decimal? HourlyRateOverride { get; init; }
}
