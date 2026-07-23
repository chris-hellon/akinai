namespace Akinai.Shared.Dtos.ClientPortal;

public record PaymentHistoryDto(
    Guid InvoiceId,
    string InvoiceNumber,
    DateOnly PaidDate,
    decimal Amount,
    string Currency);
