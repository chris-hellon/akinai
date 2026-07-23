namespace Akinai.Shared.Dtos.RecurringInvoices;

public record RecurringInvoiceLineItemDto(
    Guid Id,
    Guid? ProductId,
    string? ProductName,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    string Currency,
    int Order,
    decimal LineTotal);
