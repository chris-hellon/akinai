using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.Jobs;

/// <summary>Re-opens a completed job (sets it back to Active).</summary>
[Route("api/jobs/{Id}/reopen")]
public class ReopenJob : IPost
{
    [RouteParam]
    public Guid Id { get; init; }
}
