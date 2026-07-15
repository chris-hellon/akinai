using InvoiceSoftware.Domain.Common;

namespace InvoiceSoftware.Domain.Entities;

/// <summary>
/// The invoice's total converted into a specific currency, snapshotted at save time.
/// One row per currency the owner's clients use, so the dashboard can report in any of
/// them without re-converting (and without historical totals drifting when rates change).
/// </summary>
public class InvoiceCurrencyTotal : BaseEntity
{
    public Guid InvoiceId { get; private set; }
    public string Currency { get; private set; } = null!;
    public decimal Amount { get; private set; }

    public Invoice Invoice { get; private set; } = null!;

    private InvoiceCurrencyTotal() { }

    public static InvoiceCurrencyTotal Create(Guid invoiceId, string currency, decimal amount)
        => new()
        {
            InvoiceId = invoiceId,
            Currency = currency,
            Amount = Math.Round(amount, 2)
        };
}
