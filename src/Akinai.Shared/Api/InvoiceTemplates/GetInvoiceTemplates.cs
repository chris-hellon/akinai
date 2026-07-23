using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.InvoiceTemplates;

namespace Akinai.Shared.Api.InvoiceTemplates;

[Route("api/invoice-templates")]
public class GetInvoiceTemplates : IGet<List<InvoiceTemplateDto>>
{
    [QueryStringParam]
    public bool IncludeSystem { get; init; } = true;
}
