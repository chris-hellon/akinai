using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.Invoices;

[Route("api/invoices/{Id}/email-template")]
public class GetInvoiceEmailTemplate : IGet<string?>
{
    [RouteParam] public Guid Id { get; init; }
}
