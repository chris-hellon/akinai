using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.Products;

[Route("api/products")]
public class CreateProduct : IPost<Guid>
{
    [BodyParam]
    public string Name { get; init; } = null!;

    [BodyParam]
    public string? Description { get; init; }

    [BodyParam]
    public decimal UnitPrice { get; init; }

    [BodyParam]
    public string Currency { get; init; } = "USD";

    [BodyParam]
    public decimal DefaultQuantity { get; init; } = 1;

    [BodyParam]
    public string? Category { get; init; }

    [BodyParam]
    public string? Sku { get; init; }

    [BodyParam]
    public decimal? TaxRate { get; init; }
}
