using BlazorUtils.EasyApi;
using InvoiceSoftware.Shared.Dtos.Dashboard;

namespace InvoiceSoftware.Shared.Api.Dashboard;

[Route("api/dashboard/invoice-status")]
public class GetInvoiceStatusBreakdown : IGet<InvoiceStatusBreakdownDto>
{
    [QueryStringParam]
    public string? ReportingCurrency { get; init; }
}
