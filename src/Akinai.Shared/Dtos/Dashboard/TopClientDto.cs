namespace Akinai.Shared.Dtos.Dashboard;

public record TopClientDto(
    Guid ClientId,
    string ClientName,
    string? CompanyName,
    decimal TotalRevenue,
    int InvoiceCount,
    decimal OutstandingAmount,
    string Currency);
