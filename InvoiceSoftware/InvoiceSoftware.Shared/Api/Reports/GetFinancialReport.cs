using BlazorUtils.EasyApi;
using InvoiceSoftware.Shared.Dtos.Reports;

namespace InvoiceSoftware.Shared.Api.Reports;

[Route("api/reports/financial")]
public class GetFinancialReport : IGet<FinancialReportDto>
{
    /// <summary>month | quarter | year | last-year. Ignored when FromDate/ToDate are supplied.</summary>
    [QueryStringParam]
    public string Period { get; init; } = "year";

    [QueryStringParam]
    public DateOnly? FromDate { get; init; }

    [QueryStringParam]
    public DateOnly? ToDate { get; init; }

    /// <summary>Currency to report all figures in. Defaults to the business default currency.</summary>
    [QueryStringParam]
    public string? ReportingCurrency { get; init; }
}
