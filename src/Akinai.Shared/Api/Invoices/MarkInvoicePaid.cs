using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.Invoices;

[Route("api/invoices/{Id}/mark-paid")]
public class MarkInvoicePaid : IPost
{
    [RouteParam]
    public Guid Id { get; init; }

    [BodyParam]
    public DateOnly PaidDate { get; init; }
}
