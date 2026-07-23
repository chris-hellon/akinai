using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.Estimates;

namespace Akinai.Shared.Api.Estimates;

[Route("api/estimates/public/{Token}")]
public class GetPublicEstimate : IGet<PublicEstimateDto?>
{
    [RouteParam]
    public Guid Token { get; init; }
}
