using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.Clients;

namespace Akinai.Shared.Api.Clients;

[Route("api/clients/{Id}")]
public class GetClient : IGet<ClientDetailDto?>
{
    [RouteParam]
    public Guid Id { get; init; }
}
