namespace Akinai.Infrastructure.Services;

public class ExchangeRateOptions
{
    public const string SectionName = "ExchangeRate";

    /// <summary>API key for exchangerate-api.com. When empty, fallback rates are used.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Whether to attempt live rate fetching at all.</summary>
    public bool Enabled { get; set; } = true;
}
