using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.Jobs;

namespace Akinai.Shared.Api.Jobs;

[Route("api/jobs/{JobId}/tasks/{TaskId}/toggle")]
public class ToggleJobTaskComplete : IPost<JobTaskDto>
{
    [RouteParam]
    public Guid JobId { get; init; }

    [RouteParam]
    public Guid TaskId { get; init; }
}
