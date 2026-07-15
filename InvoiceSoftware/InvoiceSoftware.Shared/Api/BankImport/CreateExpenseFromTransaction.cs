using BlazorUtils.EasyApi;

namespace InvoiceSoftware.Shared.Api.BankImport;

[Route("api/bank-import/transactions/{Id}/expense")]
public class CreateExpenseFromTransaction : IPost<Guid>
{
    [RouteParam]
    public Guid Id { get; init; }

    [BodyParam]
    public string Category { get; init; } = "Other";

    [BodyParam]
    public string? Notes { get; init; }

    [BodyParam]
    public bool IsBillable { get; init; }

    [BodyParam]
    public Guid? ClientId { get; init; }

    [BodyParam]
    public Guid? ProjectId { get; init; }
}
