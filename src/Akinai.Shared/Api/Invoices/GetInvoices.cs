using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.Invoices;

namespace Akinai.Shared.Api.Invoices;

[Route("api/invoices")]
public class GetInvoices : IGet<PaginatedResponse<InvoiceSummaryDto>>, IPaginatedRequest
{
    [QueryStringParam]
    public int Page { get; set; } = 1;

    [QueryStringParam]
    public int PageSize { get; set; } = 15;

    [QueryStringParam]
    public string? Search { get; set; }

    [QueryStringParam]
    public string? Status { get; init; }
}
