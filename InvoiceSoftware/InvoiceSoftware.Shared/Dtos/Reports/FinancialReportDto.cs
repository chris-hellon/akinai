namespace InvoiceSoftware.Shared.Dtos.Reports;

public record FinancialReportDto(
    string Currency,
    DateOnly FromDate,
    DateOnly ToDate,
    decimal Income,
    decimal ExpenseTotal,
    decimal NetProfit,
    decimal TaxCollected,
    decimal TaxPaid,
    decimal OutstandingTotal,
    decimal OverdueTotal,
    int PaidInvoiceCount,
    int ExpenseCount,
    List<ReportCategoryDto> ExpensesByCategory,
    List<ReportMonthlyDto> Monthly,
    List<ReportAgingBucketDto> Aging,
    List<ReportTopClientDto> TopClients);

/// <summary>Category is the raw enum name (e.g. "OfficeSupplies"); format on the client.</summary>
public record ReportCategoryDto(string Category, decimal Amount);

public record ReportMonthlyDto(string Label, decimal Income, decimal Expenses, decimal Profit);

public record ReportAgingBucketDto(string Label, int Count, decimal Amount);

public record ReportTopClientDto(Guid ClientId, string ClientName, decimal Amount, int InvoiceCount);
