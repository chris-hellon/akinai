namespace Akinai.Shared.Dtos.Expenses;

public record ExpenseSummaryDto(
    Guid Id,
    string Category,
    string MerchantName,
    DateOnly ExpenseDate,
    string PaymentMethod,
    decimal Amount,
    string Currency,
    decimal TaxAmount,
    bool IsBillable,
    bool IsBilled,
    Guid? ClientId,
    string? ClientName,
    Guid? ProjectId,
    string? ProjectName,
    string? GroupName,
    Guid? RecurringExpenseId);
