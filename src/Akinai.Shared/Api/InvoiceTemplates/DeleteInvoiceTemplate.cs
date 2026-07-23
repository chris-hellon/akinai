using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.InvoiceTemplates;

[Route("api/invoice-templates/{Id}")]
public class DeleteInvoiceTemplate : IDelete
{
    [RouteParam]
    public Guid Id { get; init; }
}
