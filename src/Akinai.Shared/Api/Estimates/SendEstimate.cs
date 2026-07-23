using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.Estimates;

[Route("api/estimates/{Id}/send")]
public class SendEstimate : IPost
{
    [RouteParam]
    public Guid Id { get; init; }
}
