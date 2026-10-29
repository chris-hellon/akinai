using Akinai.Domain.Common;

namespace Akinai.Domain.Entities;

/// <summary>
/// A currency, besides the one it is billed in, that this invoice will accept payment in,
/// with the amount it is accepted at.
///
/// Deliberately not the same thing as <see cref="InvoiceCurrencyTotal"/>, which looks
/// almost identical. A currency total is a reporting conversion, written for every
/// currency the owner's clients happen to use; this is a commercial offer the owner made
/// on one invoice, and it is only ever here because they ticked it. Conflating the two
/// would have put "pay in USD" on an invoice the moment an unrelated USD client was added.
///
/// The amount is snapshotted at save, like every other money figure on an invoice: the
/// client is owed the number they were shown, not that number re-converted at today's
/// rate when they get round to paying.
/// </summary>
public class InvoicePaymentCurrency : BaseEntity
{
    public Guid InvoiceId { get; private set; }
    public string Currency { get; private set; } = null!;
    public decimal Amount { get; private set; }

    public Invoice Invoice { get; private set; } = null!;

    private InvoicePaymentCurrency() { }

    public static InvoicePaymentCurrency Create(Guid invoiceId, string currency, decimal amount)
    {
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("A payment currency needs a currency code.", nameof(currency));

        return new InvoicePaymentCurrency
        {
            InvoiceId = invoiceId,
            Currency = currency.Trim().ToUpperInvariant(),
            // VND and the other zero-decimal currencies still round to 2 here, matching
            // InvoiceCurrencyTotal. The display layer is what drops the decimals.
            Amount = Math.Round(amount, 2),
        };
    }
}
