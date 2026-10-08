using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.BankImport;

/// <summary>
/// Marks the withholding certificate for a matched payment as received. The outstanding
/// item on a withheld payment is paperwork, not money — and it is worth real cash at year
/// end, because it is what lets the tax be credited rather than written off.
/// </summary>
[Route("api/bank-import/{Id}/withholding-certificate")]
public class RecordWithholdingCertificate : IPost
{
    [RouteParam]
    public Guid Id { get; init; }
}
