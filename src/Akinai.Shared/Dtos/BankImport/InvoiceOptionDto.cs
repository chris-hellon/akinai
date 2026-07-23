namespace Akinai.Shared.Dtos.BankImport;

public record InvoiceOptionDto(
    Guid Id,
    string InvoiceNumber,
    string ClientName,
    decimal Total,
    string Currency,
    string Status);
