namespace Akinai.Shared.Dtos.Dashboard;

public record DashboardSummaryDto(
    decimal UnpaidInvoicesTotal,
    int UnpaidInvoicesCount,
    decimal ThisWeekHours,
    decimal ThisMonthRevenue,
    int ActiveProjectsCount,
    int ActiveJobsCount);
