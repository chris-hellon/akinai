using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.InvoiceTemplates;

[Route("api/invoice-templates/{Id}/default")]
public class SetDefaultTemplate : IPost
{
    [RouteParam]
    public Guid Id { get; init; }
}
