using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.Estimates;

namespace Akinai.Shared.Api.Estimates;

[Route("api/estimates/{Id}")]
public class GetEstimate : IGet<EstimateDetailDto?>
{
    [RouteParam]
    public Guid Id { get; init; }
}
