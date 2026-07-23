namespace Akinai.Shared.Dtos.BankImport;

public record TransactionMatchDto(
    Guid InvoiceId,
    string InvoiceNumber,
    string ClientName,
    decimal InvoiceTotal,
    string Currency,
    DateOnly DueDate,
    string Status,
    string MatchConfidence,
    string MatchReason);
