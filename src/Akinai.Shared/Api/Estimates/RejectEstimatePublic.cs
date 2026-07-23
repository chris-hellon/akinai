using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.Estimates;

[Route("api/estimates/public/{Token}/reject")]
public class RejectEstimatePublic : IPost
{
    [RouteParam]
    public Guid Token { get; init; }

    [BodyParam]
    public string? Reason { get; init; }
}
