using Akinai.Shared.Dtos.CurrencyPaymentSettings;

namespace Akinai.Shared.Payments;

/// <summary>
/// One currency an invoice can be settled in besides the one it is billed in,
/// with the amount snapshotted at save time and the account that receives it.
/// </summary>
public record InvoicePaymentCurrencyDto(
    string Currency,
    decimal Amount,
    CurrencyPaymentSettingsDto Settings);

/// <summary>
/// Which currencies an invoice should actually print a payment route for.
///
/// The choice itself is made per invoice, when it is written, and stored as
/// InvoicePaymentCurrency rows — it is a commercial offer, not something to infer. This
/// is the second gate, applied at render: an account the owner has since emptied or
/// deleted must stop being advertised, because an invoice that names a currency with no
/// account behind it sends the client looking for details that are not on the page.
///
/// It is applied identically in the owner's view, the client's copy and the PDF, so all
/// three offer the same routes.
/// </summary>
public static class AlternatePaymentCurrencies
{
    /// <summary>
    /// Whether these settings name an account a payment could actually reach. A row can
    /// exist with nothing but a bank name on it — the currency tab was opened and never
    /// filled in — and that must not surface as a way to pay.
    /// </summary>
    public static bool IsPayable(CurrencyPaymentSettingsDto? s) =>
        s is not null &&
        (!string.IsNullOrWhiteSpace(s.BankAccountNumber)
         || !string.IsNullOrWhiteSpace(s.BankIban)
         || !string.IsNullOrWhiteSpace(s.UpiVpa)
         || !string.IsNullOrWhiteSpace(s.PixKey)
         || !string.IsNullOrWhiteSpace(s.PromptPayId));

    /// <param name="invoiceCurrency">Billed in. Excluded — it is the primary block.</param>
    /// <param name="currencyTotals">The invoice's accepted currencies and their snapshotted amounts.</param>
    /// <param name="settings">Every currency account the owner has configured.</param>
    public static IReadOnlyList<InvoicePaymentCurrencyDto> Select(
        string? invoiceCurrency,
        IReadOnlyDictionary<string, decimal> currencyTotals,
        IEnumerable<CurrencyPaymentSettingsDto> settings)
    {
        var byCurrency = new Dictionary<string, CurrencyPaymentSettingsDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in settings)
        {
            if (string.IsNullOrWhiteSpace(s.CurrencyCode)) continue;
            byCurrency[s.CurrencyCode] = s;
        }

        var result = new List<InvoicePaymentCurrencyDto>();
        foreach (var (currency, amount) in currencyTotals)
        {
            if (string.IsNullOrWhiteSpace(currency)) continue;
            if (string.Equals(currency, invoiceCurrency, StringComparison.OrdinalIgnoreCase)) continue;
            if (!byCurrency.TryGetValue(currency, out var s) || !IsPayable(s)) continue;

            result.Add(new InvoicePaymentCurrencyDto(currency.ToUpperInvariant(), amount, s));
        }

        // Alphabetical, so the same invoice lists its options in the same order in the
        // view, the client's copy and the PDF. Dictionary order would not promise that.
        return result.OrderBy(r => r.Currency, StringComparer.Ordinal).ToList();
    }
}
