using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.Clients;

namespace Akinai.Shared.Api.Clients;

[Route("api/clients")]
public class GetClients : IGet<PaginatedResponse<ClientDto>>, IPaginatedRequest
{
    [QueryStringParam]
    public int Page { get; set; } = 1;

    [QueryStringParam]
    public int PageSize { get; set; } = 15;

    [QueryStringParam]
    public string? Search { get; set; }

    [QueryStringParam]
    public bool ActiveOnly { get; init; } = true;
}
