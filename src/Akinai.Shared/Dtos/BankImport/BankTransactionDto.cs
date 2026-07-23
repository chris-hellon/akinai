namespace Akinai.Shared.Dtos.BankImport;

public record BankTransactionDto(
    Guid Id,
    DateOnly TransactionDate,
    string Description,
    decimal Amount,
    string Currency,
    string? Reference,
    string? BankAccountName,
    Guid? MatchedInvoiceId,
    string? MatchedInvoiceNumber,
    string MatchConfidence,
    string? MatchNotes,
    bool IsMatched,
    bool IsIgnored,
    string? IgnoreReason,
    DateTime ImportedAt,
    string SourceFileName,
    Guid? ExpenseId = null,
    bool IsExpensed = false);
