using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.Products;

namespace Akinai.Shared.Api.Products;

[Route("api/products")]
public class GetProducts : IGet<PaginatedResponse<ProductDto>>, IPaginatedRequest
{
    [QueryStringParam]
    public int Page { get; set; } = 1;

    [QueryStringParam]
    public int PageSize { get; set; } = 15;

    [QueryStringParam]
    public string? Search { get; set; }

    [QueryStringParam]
    public bool ActiveOnly { get; init; } = true;

    [QueryStringParam]
    public string? Category { get; init; }
}
