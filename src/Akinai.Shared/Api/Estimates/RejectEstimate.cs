using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.Estimates;

[Route("api/estimates/{Id}/reject")]
public class RejectEstimate : IPost
{
    [RouteParam]
    public Guid Id { get; init; }
}
