namespace Akinai.Shared.Dtos.Dashboard;

public record DashboardMetricsDto(
    decimal TotalRevenue,
    decimal PaidRevenue,
    decimal OutstandingAmount,
    decimal OverdueAmount,
    int TotalInvoices,
    int PaidInvoices,
    int OutstandingInvoices,
    int OverdueInvoices,
    decimal BillableHours,
    decimal UnbilledHours,
    decimal UnbilledAmount,
    int ActiveClients,
    int ActiveProjects);
