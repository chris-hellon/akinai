namespace InvoiceSoftware.Infrastructure.Banking;

/// <summary>Shared ISO 3166 country-code sets for provider coverage (lowercase).</summary>
public static class BankingCountries
{
    public static readonly IReadOnlyCollection<string> UkEu = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "gb", "ie", "de", "fr", "es", "nl", "it", "be", "at", "pt",
        "pl", "fi", "dk", "se", "no", "ee", "lv", "lt"
    };

    /// <summary>Plaid: North America + UK/EU.</summary>
    public static readonly IReadOnlyCollection<string> Plaid = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "us", "ca", "gb", "ie", "de", "fr", "es", "nl", "it", "be",
        "at", "pt", "pl", "dk", "se", "no", "fi", "ee", "lv", "lt"
    };
}
