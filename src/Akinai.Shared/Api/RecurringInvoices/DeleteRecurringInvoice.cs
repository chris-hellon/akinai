using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.RecurringInvoices;

[Route("api/recurring-invoices/{Id}")]
public class DeleteRecurringInvoice : IDelete
{
    [RouteParam]
    public Guid Id { get; init; }
}
