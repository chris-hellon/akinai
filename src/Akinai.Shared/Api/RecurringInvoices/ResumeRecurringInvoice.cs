using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.RecurringInvoices;

[Route("api/recurring-invoices/{Id}/resume")]
public class ResumeRecurringInvoice : IPost
{
    [RouteParam]
    public Guid Id { get; init; }
}
