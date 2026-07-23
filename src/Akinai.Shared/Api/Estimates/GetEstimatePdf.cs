using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.Estimates;

[Route("api/estimates/{Id}/pdf")]
public class GetEstimatePdf : IGet<byte[]?>
{
    [RouteParam]
    public Guid Id { get; init; }
}
