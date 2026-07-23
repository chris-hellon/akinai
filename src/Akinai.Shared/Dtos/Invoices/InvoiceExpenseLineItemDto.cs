namespace Akinai.Shared.Dtos.Invoices;

public record InvoiceExpenseLineItemDto(
    Guid Id,
    string Category,
    string MerchantName,
    DateOnly ExpenseDate,
    string Description,
    decimal Amount,
    decimal TaxAmount,
    decimal LineTotal,
    string Currency,
    string? ProjectName);
