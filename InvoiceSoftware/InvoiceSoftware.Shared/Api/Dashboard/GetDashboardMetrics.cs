using BlazorUtils.EasyApi;
using InvoiceSoftware.Shared.Dtos.Dashboard;

namespace InvoiceSoftware.Shared.Api.Dashboard;

[Route("api/dashboard/metrics")]
public class GetDashboardMetrics : IGet<DashboardMetricsDto>
{
    [QueryStringParam]
    public string Period { get; init; } = "month"; // week, month, quarter, year

    [QueryStringParam]
    public DateOnly? FromDate { get; init; }

    [QueryStringParam]
    public DateOnly? ToDate { get; init; }

    /// <summary>Currency to report all figures in. Defaults to the business default currency.</summary>
    [QueryStringParam]
    public string? ReportingCurrency { get; init; }
}
