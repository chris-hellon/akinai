namespace Akinai.Infrastructure.Services;

/// <summary>
/// Fetches USD-based exchange rates and converts amounts between currencies.
/// Live rates come from exchangerate-api.com; falls back to sane defaults when no
/// API key is configured or the service is unavailable.
/// </summary>
public interface IExchangeRateService
{
    /// <summary>
    /// USD-based conversion rates: 1 USD = value units of the keyed currency.
    /// Always contains "USD" = 1.
    /// </summary>
    Task<IReadOnlyDictionary<string, decimal>> GetUsdRatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts an amount from one currency to another. Returns null if either rate is unknown.
    /// </summary>
    Task<decimal?> ConvertAsync(decimal amount, string fromCurrency, string toCurrency, CancellationToken cancellationToken = default);
}
