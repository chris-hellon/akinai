using BlazorUtils.EasyApi;
using InvoiceSoftware.Shared.Dtos.Dashboard;

namespace InvoiceSoftware.Shared.Api.Dashboard;

[Route("api/dashboard/revenue-chart")]
public class GetRevenueChart : IGet<RevenueChartDataDto>
{
    [QueryStringParam]
    public string Period { get; init; } = "month"; // week, month, quarter, year

    [QueryStringParam]
    public int Months { get; init; } = 12;

    /// <summary>Currency to report revenue in. Defaults to the business default currency.</summary>
    [QueryStringParam]
    public string? ReportingCurrency { get; init; }
}
