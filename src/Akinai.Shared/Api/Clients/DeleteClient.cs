using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.Clients;

[Route("api/clients/{Id}")]
public class DeleteClient : IDelete
{
    [RouteParam]
    public Guid Id { get; init; }
}
