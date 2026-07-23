using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.Invoices;

/// <summary>
/// Moves a Draft invoice to Sent without emailing it (e.g. it was delivered another way).
/// </summary>
[Route("api/invoices/{Id}/mark-sent")]
public class MarkInvoiceSent : IPost
{
    [RouteParam]
    public Guid Id { get; init; }
}
