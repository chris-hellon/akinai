using BlazorUtils.EasyApi;
using InvoiceSoftware.Shared.Dtos.BankImport;

namespace InvoiceSoftware.Shared.Api.BankImport;

[Route("api/bank-import/matchable-invoices")]
public class GetMatchableInvoices : IGet<List<InvoiceOptionDto>>
{
    [QueryStringParam]
    public string? Search { get; init; }
}
