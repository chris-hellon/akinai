using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.Dashboard;

namespace Akinai.Shared.Api.Dashboard;

[Route("api/dashboard/invoice-status")]
public class GetInvoiceStatusBreakdown : IGet<InvoiceStatusBreakdownDto>
{
    [QueryStringParam]
    public string? ReportingCurrency { get; init; }
}
