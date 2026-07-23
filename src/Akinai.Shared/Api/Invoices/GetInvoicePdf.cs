using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.Invoices;

[Route("api/invoices/{Id}/pdf")]
public class GetInvoicePdf : IGet<byte[]>
{
    [RouteParam]
    public Guid Id { get; init; }
}
