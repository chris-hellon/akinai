namespace Akinai.Shared.Dtos.Invoices;

public record InvoiceSummaryDto(
    Guid Id,
    string InvoiceNumber,
    Guid ClientId,
    string ClientName,
    string Currency,
    DateOnly IssueDate,
    DateOnly DueDate,
    string Status,
    decimal Subtotal,
    decimal TaxAmount,
    decimal Total);
