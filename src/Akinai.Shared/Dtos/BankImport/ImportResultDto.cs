namespace Akinai.Shared.Dtos.BankImport;

public record ImportResultDto(
    int TotalTransactions,
    int ImportedTransactions,
    int SkippedDuplicates,
    int AutoMatchedTransactions,
    List<string> Errors);
