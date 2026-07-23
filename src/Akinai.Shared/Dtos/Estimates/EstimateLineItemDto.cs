namespace Akinai.Shared.Dtos.Estimates;

public record EstimateLineItemDto(
    Guid Id,
    Guid? ProductId,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    string Currency,
    int Order,
    decimal LineTotal);
