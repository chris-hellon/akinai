using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.RecurringInvoices;

namespace Akinai.Shared.Api.RecurringInvoices;

[Route("api/recurring-invoices/{Id}")]
public class GetRecurringInvoice : IGet<RecurringInvoiceDetailDto?>
{
    [RouteParam]
    public Guid Id { get; init; }
}
