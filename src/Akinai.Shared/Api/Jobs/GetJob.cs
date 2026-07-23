using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.Jobs;

namespace Akinai.Shared.Api.Jobs;

[Route("api/jobs/{Id}")]
public class GetJob : IGet<JobDetailDto?>
{
    [RouteParam]
    public Guid Id { get; init; }
}
