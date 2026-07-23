namespace Akinai.Shared.Dtos.CashFlow;

public record CashFlowProjectionDto(
    string Currency,
    bool HasBalance,
    decimal CurrentBalance,
    DateOnly? BalanceAsOf,
    decimal MonthlyOutflow,
    decimal ExpectedInflow30,
    int RunwayDays,               // -1 = healthy (not trending to zero) or unknown (no balance set)
    DateOnly? ZeroDate,
    List<CashProjectionPointDto> Projection,
    List<PredictedPaymentDto> ExpectedPayments,
    List<ClientReliabilityDto> ClientReliability,
    int OverdueCount,
    decimal OverdueAmount);

public record CashProjectionPointDto(DateOnly Date, decimal Balance, decimal Inflow);

public record PredictedPaymentDto(
    Guid InvoiceId,
    string InvoiceNumber,
    Guid ClientId,
    string ClientName,
    decimal Amount,
    DateOnly DueDate,
    DateOnly PredictedDate,
    string Confidence,          // High | Medium | Low | New
    string ClientScore,         // Reliable | Slow | Risky | New
    int DaysLatePredicted,
    bool IsOverdue);

public record ClientReliabilityDto(
    Guid ClientId,
    string ClientName,
    double AvgDaysLate,
    double OnTimeRate,
    int SampleSize,
    string Score);              // Reliable | Slow | Risky | New
