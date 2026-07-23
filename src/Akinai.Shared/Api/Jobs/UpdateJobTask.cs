using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.Jobs;

namespace Akinai.Shared.Api.Jobs;

[Route("api/jobs/{JobId}/tasks/{TaskId}")]
public class UpdateJobTask : IPut<JobTaskDto>
{
    [RouteParam]
    public Guid JobId { get; init; }

    [RouteParam]
    public Guid TaskId { get; init; }

    [BodyParam]
    public string Title { get; init; } = null!;

    [BodyParam]
    public string? Description { get; init; }
}
