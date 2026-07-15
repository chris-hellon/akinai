using BlazorUtils.EasyApi;
using InvoiceSoftware.Shared.Dtos.BankImport;

namespace InvoiceSoftware.Shared.Api.BankImport;

[Route("api/bank-import/invoices/{InvoiceId}/transactions")]
public class GetInvoiceBankTransactions : IGet<List<BankTransactionDto>>
{
    [RouteParam]
    public Guid InvoiceId { get; init; }
}
