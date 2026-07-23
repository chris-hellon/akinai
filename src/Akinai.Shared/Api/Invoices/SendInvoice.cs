using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.Invoices;

[Route("api/invoices/{Id}/send")]
public class SendInvoice : IPost
{
    [RouteParam]
    public Guid Id { get; init; }

    [BodyParam]
    public string Email { get; init; } = null!;

    [BodyParam]
    public string Subject { get; init; } = null!;

    [BodyParam]
    public string Body { get; init; } = null!;
}
