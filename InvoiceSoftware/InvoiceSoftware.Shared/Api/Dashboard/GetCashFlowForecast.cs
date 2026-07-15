using BlazorUtils.EasyApi;
using InvoiceSoftware.Shared.Dtos.Dashboard;

namespace InvoiceSoftware.Shared.Api.Dashboard;

[Route("api/dashboard/cash-flow")]
public class GetCashFlowForecast : IGet<CashFlowForecastDto>
{
    [QueryStringParam]
    public int Months { get; init; } = 3;

    [QueryStringParam]
    public string? ReportingCurrency { get; init; }
}
