using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.Products;

[Route("api/products/{Id}")]
public class DeleteProduct : IDelete
{
    [RouteParam]
    public Guid Id { get; init; }
}
