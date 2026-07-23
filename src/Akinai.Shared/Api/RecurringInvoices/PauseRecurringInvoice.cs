using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.RecurringInvoices;

[Route("api/recurring-invoices/{Id}/pause")]
public class PauseRecurringInvoice : IPost
{
    [RouteParam]
    public Guid Id { get; init; }
}
