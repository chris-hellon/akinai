using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.BankImport;

namespace Akinai.Shared.Api.BankImport;

[Route("api/bank-import/invoices/{InvoiceId}/transactions")]
public class GetInvoiceBankTransactions : IGet<List<BankTransactionDto>>
{
    [RouteParam]
    public Guid InvoiceId { get; init; }
}
