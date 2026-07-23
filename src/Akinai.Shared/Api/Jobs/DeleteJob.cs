using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.Jobs;

[Route("api/jobs/{Id}")]
public class DeleteJob : IDelete
{
    [RouteParam]
    public Guid Id { get; init; }
}
