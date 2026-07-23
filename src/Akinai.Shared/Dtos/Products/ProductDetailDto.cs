namespace Akinai.Shared.Dtos.Products;

public record ProductDetailDto(
    Guid Id,
    string Name,
    string? Description,
    decimal UnitPrice,
    string Currency,
    decimal DefaultQuantity,
    string? Category,
    string? Sku,
    decimal? TaxRate,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? ModifiedAt);
