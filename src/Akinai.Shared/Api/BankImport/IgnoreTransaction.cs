using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.BankImport;

[Route("api/bank-import/transactions/{Id}/ignore")]
public class IgnoreTransaction : IPost
{
    [RouteParam]
    public Guid Id { get; init; }

    [BodyParam]
    public string? Reason { get; init; }
}
