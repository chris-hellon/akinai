using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.InvoiceTemplates;

namespace Akinai.Shared.Api.InvoiceTemplates;

[Route("api/invoice-templates/{Id}")]
public class GetInvoiceTemplate : IGet<InvoiceTemplateDto?>
{
    [RouteParam]
    public Guid Id { get; init; }
}
