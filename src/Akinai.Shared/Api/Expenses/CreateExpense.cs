using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.Expenses;

[Route("api/expenses")]
public class CreateExpense : IPost<Guid>
{
    [BodyParam]
    public string Category { get; init; } = null!;

    [BodyParam]
    public string MerchantName { get; init; } = null!;

    [BodyParam]
    public DateOnly ExpenseDate { get; init; }

    [BodyParam]
    public string PaymentMethod { get; init; } = null!;

    [BodyParam]
    public decimal Amount { get; init; }

    [BodyParam]
    public string Currency { get; init; } = "USD";

    [BodyParam]
    public decimal TaxAmount { get; init; }

    [BodyParam]
    public bool IsTaxInclusive { get; init; }

    [BodyParam]
    public string? MerchantTaxNumber { get; init; }

    [BodyParam]
    public string? GroupName { get; init; }

    [BodyParam]
    public string? Notes { get; init; }

    [BodyParam]
    public bool IsReimbursable { get; init; }

    [BodyParam]
    public bool IsBillable { get; init; }

    [BodyParam]
    public Guid? ClientId { get; init; }

    [BodyParam]
    public Guid? ProjectId { get; init; }
}
