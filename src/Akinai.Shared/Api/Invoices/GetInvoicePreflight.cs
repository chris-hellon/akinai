using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.Preflight;

namespace Akinai.Shared.Api.Invoices;

[Route("api/invoices/{Id}/preflight")]
public class GetInvoicePreflight : IGet<InvoicePreflightDto?>
{
    [RouteParam]
    public Guid Id { get; init; }
}
