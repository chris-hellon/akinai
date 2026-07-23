using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.ClientPortal;

namespace Akinai.Shared.Api.ClientPortal;

[Route("api/portal/{Token}")]
public class GetClientPortalSummary : IGet<ClientPortalSummaryDto?>
{
    [RouteParam]
    public Guid Token { get; init; }
}
