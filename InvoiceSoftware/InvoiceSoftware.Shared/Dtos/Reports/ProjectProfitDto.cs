namespace InvoiceSoftware.Shared.Dtos.Reports;

public record ProjectProfitDto(
    Guid ProjectId,
    string ProjectName,
    string ClientName,
    decimal Revenue,
    decimal Expenses,
    decimal Profit,
    double BilledHours);
