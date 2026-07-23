using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.Jobs;

[Route("api/jobs/{JobId}/tasks/{TaskId}")]
public class DeleteJobTask : IDelete
{
    [RouteParam]
    public Guid JobId { get; init; }

    [RouteParam]
    public Guid TaskId { get; init; }
}
