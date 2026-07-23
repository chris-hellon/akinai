namespace Akinai.Shared.Dtos.Reports;

public record ClientProfitDto(
    Guid ClientId,
    string ClientName,
    decimal Revenue,
    decimal Expenses,
    decimal Profit,
    decimal Outstanding,
    int InvoiceCount);
