namespace Akinai.Shared.Dtos.Invoices;

public record InvoiceLineItemDto(
    Guid Id,
    Guid JobId,
    string JobName,
    string ProjectName,
    string? SectionName,
    DateOnly? Date,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    string Currency);
