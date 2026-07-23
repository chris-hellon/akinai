using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.Jobs;

[Route("api/jobs")]
public class CreateJob : IPost<Guid>
{
    [BodyParam]
    public Guid ProjectId { get; init; }

    [BodyParam]
    public Guid? SectionId { get; init; }

    [BodyParam]
    public string Name { get; init; } = null!;

    [BodyParam]
    public string? Description { get; init; }

    [BodyParam]
    public string? Notes { get; init; }

    [BodyParam]
    public string Priority { get; init; } = "Medium";

    [BodyParam]
    public DateOnly? StartDate { get; init; }

    [BodyParam]
    public DateOnly? DueDate { get; init; }

    [BodyParam]
    public decimal? EstimatedHours { get; init; }

    [BodyParam]
    public decimal? HourlyRateOverride { get; init; }
}
