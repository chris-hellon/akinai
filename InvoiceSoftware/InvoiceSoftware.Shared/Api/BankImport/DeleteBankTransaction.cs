using BlazorUtils.EasyApi;

namespace InvoiceSoftware.Shared.Api.BankImport;

[Route("api/bank-import/transactions/{Id}")]
public class DeleteBankTransaction : IDelete
{
    [RouteParam]
    public Guid Id { get; init; }
}
