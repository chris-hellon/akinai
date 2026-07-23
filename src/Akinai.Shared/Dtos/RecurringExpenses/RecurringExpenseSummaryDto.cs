namespace Akinai.Shared.Dtos.RecurringExpenses;

public record RecurringExpenseSummaryDto(
    Guid Id,
    string Category,
    string MerchantName,
    string PaymentMethod,
    decimal Amount,
    string Currency,
    int FrequencyInterval,
    string Frequency,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool IsActive,
    DateOnly NextExpenseDate,
    int GeneratedCount,
    bool IsBillable,
    Guid? ClientId,
    string? ClientName,
    Guid? ProjectId,
    string? ProjectName);
