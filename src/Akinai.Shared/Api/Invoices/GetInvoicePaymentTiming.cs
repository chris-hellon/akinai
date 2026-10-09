using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.Timing;

namespace Akinai.Shared.Api.Invoices;

[Route("api/invoices/{Id}/payment-timing")]
public class GetInvoicePaymentTiming : IGet<PaymentTimingDto?>
{
    [RouteParam]
    public Guid Id { get; init; }
}
