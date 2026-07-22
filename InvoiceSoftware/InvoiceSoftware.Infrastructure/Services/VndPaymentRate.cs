namespace InvoiceSoftware.Infrastructure.Services;

/// <summary>
/// The VND-to-home-currency rate used when offering PayPal, Wise or Revolut on
/// a VND invoice.
///
/// This used to be a number the user typed into Invoice Settings and kept up to
/// date by hand. It is now taken from the live rate service that already backs
/// invoice totals and the cash-flow forecast, so a VND invoice offers a
/// converted amount that is current at the time it is viewed rather than
/// whenever somebody last remembered to edit the setting.
/// </summary>
public static class VndPaymentRate
{
    /// <summary>
    /// How many VND make one unit of <paramref name="homeCurrency"/>, or null
    /// when the rate is unavailable or the home currency is VND itself (in
    /// which case nothing needs converting).
    ///
    /// Returning null degrades cleanly: the payment options simply do not offer
    /// a converted amount, which is better than offering a wrong one.
    /// </summary>
    public static async Task<decimal?> GetAsync(
        IExchangeRateService exchangeRates,
        string? homeCurrency,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(homeCurrency)) return null;
        if (string.Equals(homeCurrency, "VND", StringComparison.OrdinalIgnoreCase)) return null;

        var rate = await exchangeRates.ConvertAsync(1m, homeCurrency, "VND", cancellationToken);
        return rate is > 0 ? rate : null;
    }
}
