using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.Projects;

[Route("api/projects")]
public class CreateProject : IPost<Guid>
{
    [BodyParam]
    public Guid ClientId { get; init; }

    [BodyParam]
    public string Name { get; init; } = null!;

    [BodyParam]
    public string? Description { get; init; }

    [BodyParam]
    public DateOnly? StartDate { get; init; }

    [BodyParam]
    public DateOnly? EndDate { get; init; }

    [BodyParam]
    public decimal? HourlyRateOverride { get; init; }
}
