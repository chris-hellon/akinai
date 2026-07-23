using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.BankImport;

[Route("api/bank-import/transactions/{Id}/match")]
public class MatchTransaction : IPost
{
    [RouteParam]
    public Guid Id { get; init; }

    [BodyParam]
    public Guid InvoiceId { get; init; }

    [BodyParam]
    public bool MarkInvoiceAsPaid { get; init; } = true;
}
