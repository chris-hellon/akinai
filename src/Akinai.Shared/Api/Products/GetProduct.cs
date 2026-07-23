using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.Products;

namespace Akinai.Shared.Api.Products;

[Route("api/products/{Id}")]
public class GetProduct : IGet<ProductDetailDto?>
{
    [RouteParam]
    public Guid Id { get; init; }
}
