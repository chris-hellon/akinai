using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.Estimates;

[Route("api/estimates/{Id}/accept")]
public class AcceptEstimate : IPost
{
    [RouteParam]
    public Guid Id { get; init; }
}
