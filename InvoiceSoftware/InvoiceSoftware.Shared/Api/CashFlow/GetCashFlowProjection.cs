using BlazorUtils.EasyApi;
using InvoiceSoftware.Shared.Dtos.CashFlow;

namespace InvoiceSoftware.Shared.Api.CashFlow;

[Route("api/cash-flow/projection")]
public class GetCashFlowProjection : IGet<CashFlowProjectionDto>
{
    /// <summary>Days to project forward. Defaults to 90.</summary>
    [QueryStringParam]
    public int Days { get; init; } = 90;

    [QueryStringParam]
    public string? ReportingCurrency { get; init; }
}
