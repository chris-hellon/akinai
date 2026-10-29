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
    decimal Total,
    // Whether this invoice actually reached anyone, which its status cannot answer:
    // Void and Paid are both reachable straight from Draft. The delete dialog needs to
    // know before it tells the user what deleting it would destroy.
    DateTime? SentAtUtc);
