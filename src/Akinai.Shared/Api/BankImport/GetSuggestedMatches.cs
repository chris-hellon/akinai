using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.BankImport;

namespace Akinai.Shared.Api.BankImport;

[Route("api/bank-import/transactions/{Id}/suggestions")]
public class GetSuggestedMatches : IGet<List<TransactionMatchDto>>
{
    [RouteParam]
    public Guid Id { get; init; }
}
