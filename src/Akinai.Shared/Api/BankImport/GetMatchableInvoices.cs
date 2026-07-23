using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.BankImport;

namespace Akinai.Shared.Api.BankImport;

[Route("api/bank-import/matchable-invoices")]
public class GetMatchableInvoices : IGet<List<InvoiceOptionDto>>
{
    [QueryStringParam]
    public string? Search { get; init; }
}
