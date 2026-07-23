using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.Jobs;

/// <summary>Marks a job as completed.</summary>
[Route("api/jobs/{Id}/complete")]
public class MarkJobComplete : IPost
{
    [RouteParam]
    public Guid Id { get; init; }
}
