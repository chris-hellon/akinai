namespace Akinai.Shared.Dtos.Invoices;

public record ProductLineItemRequest(
    Guid? ProductId,
    string Description,
    decimal Quantity,
    decimal UnitPrice);
