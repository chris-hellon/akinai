using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.RecurringInvoices;

namespace Akinai.Shared.Api.RecurringInvoices;

[Route("api/recurring-invoices")]
public class GetRecurringInvoices : IGet<PaginatedResponse<RecurringInvoiceSummaryDto>>, IPaginatedRequest
{
    [QueryStringParam]
    public int Page { get; set; } = 1;

    [QueryStringParam]
    public int PageSize { get; set; } = 15;

    [QueryStringParam]
    public string? Search { get; set; }

    [QueryStringParam]
    public bool ActiveOnly { get; init; } = true;

    [QueryStringParam]
    public Guid? ClientId { get; init; }
}
