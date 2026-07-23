namespace Akinai.Shared.Dtos.Invoices;

public record InvoiceProductLineItemDto(
    Guid Id,
    Guid? ProductId,
    string? ProductName,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    string Currency,
    int Order);
