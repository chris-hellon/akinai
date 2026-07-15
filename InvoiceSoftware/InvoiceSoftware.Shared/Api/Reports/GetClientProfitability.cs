using BlazorUtils.EasyApi;
using InvoiceSoftware.Shared.Dtos.Reports;

namespace InvoiceSoftware.Shared.Api.Reports;

[Route("api/reports/client-profitability")]
public class GetClientProfitability : IGet<List<ClientProfitDto>>
{
    [QueryStringParam]
    public string Period { get; init; } = "year";

    [QueryStringParam]
    public DateOnly? FromDate { get; init; }

    [QueryStringParam]
    public DateOnly? ToDate { get; init; }

    [QueryStringParam]
    public string? ReportingCurrency { get; init; }
}
