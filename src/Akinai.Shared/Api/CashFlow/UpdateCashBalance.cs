using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.CashFlow;

[Route("api/cash-flow/balance")]
public class UpdateCashBalance : IPost
{
    [BodyParam]
    public decimal Balance { get; init; }
}
