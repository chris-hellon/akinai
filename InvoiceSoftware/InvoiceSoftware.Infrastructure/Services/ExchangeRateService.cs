using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvoiceSoftware.Infrastructure.Services;

public class ExchangeRateService(
    HttpClient httpClient,
    ILogger<ExchangeRateService> logger,
    IOptions<ExchangeRateOptions> options) : IExchangeRateService
{
    private readonly ExchangeRateOptions _options = options.Value;

    // USD-based fallback rates (1 USD = X of currency). Used when no API key / API is down.
    private static readonly IReadOnlyDictionary<string, decimal> FallbackRates =
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["USD"] = 1m,
            ["EUR"] = 0.92m,
            ["GBP"] = 0.79m,
            ["VND"] = 25400m,
            ["JPY"] = 157m,
            ["AUD"] = 1.52m,
            ["CAD"] = 1.37m,
            ["SGD"] = 1.35m,
            ["CNY"] = 7.2m,
            ["THB"] = 36m,
            ["INR"] = 83m,
            ["NZD"] = 1.64m,
            ["CHF"] = 0.9m,
            ["HKD"] = 7.8m,
        };

    private static IReadOnlyDictionary<string, decimal>? _cachedRates;
    private static DateTime _cacheExpiry = DateTime.MinValue;
    private static readonly SemaphoreSlim _cacheLock = new(1, 1);

    public async Task<IReadOnlyDictionary<string, decimal>> GetUsdRatesAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedRates is not null && DateTime.UtcNow < _cacheExpiry)
            return _cachedRates;

        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedRates is not null && DateTime.UtcNow < _cacheExpiry)
                return _cachedRates;

            if (_options.Enabled && !string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                var live = await FetchFromApiAsync(cancellationToken);
                if (live is not null)
                {
                    _cachedRates = live;
                    _cacheExpiry = DateTime.UtcNow.AddMinutes(5);
                    return live;
                }
            }

            _cachedRates = FallbackRates;
            _cacheExpiry = DateTime.UtcNow.AddMinutes(1);
            return FallbackRates;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public async Task<decimal?> ConvertAsync(decimal amount, string fromCurrency, string toCurrency, CancellationToken cancellationToken = default)
    {
        if (string.Equals(fromCurrency, toCurrency, StringComparison.OrdinalIgnoreCase))
            return amount;

        var rates = await GetUsdRatesAsync(cancellationToken);
        if (!rates.TryGetValue(fromCurrency, out var fromRate) || fromRate <= 0)
            return null;
        if (!rates.TryGetValue(toCurrency, out var toRate) || toRate <= 0)
            return null;

        var amountInUsd = amount / fromRate;
        return Math.Round(amountInUsd * toRate, 2);
    }

    private async Task<IReadOnlyDictionary<string, decimal>?> FetchFromApiAsync(CancellationToken cancellationToken)
    {
        try
        {
            var url = $"https://v6.exchangerate-api.com/v6/{_options.ApiKey}/latest/USD";
            var response = await httpClient.GetFromJsonAsync<ExchangeRateApiResponse>(url, cancellationToken);

            if (response?.Result == "success" && response.ConversionRates is { Count: > 0 })
            {
                logger.LogInformation("Fetched {Count} live exchange rates from exchangerate-api.com", response.ConversionRates.Count);
                return new Dictionary<string, decimal>(response.ConversionRates, StringComparer.OrdinalIgnoreCase);
            }

            logger.LogWarning("Exchange rate API returned an unexpected response; using fallback rates");
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch live exchange rates; using fallback rates");
            return null;
        }
    }

    private sealed class ExchangeRateApiResponse
    {
        [JsonPropertyName("result")]
        public string? Result { get; set; }

        [JsonPropertyName("conversion_rates")]
        public Dictionary<string, decimal>? ConversionRates { get; set; }
    }
}
