namespace Akinai.Shared.Geography;

/// <summary>
/// ISO-3166 alpha-2 codes, with the one extra fact the invoice rules need: whether a
/// country is in the EU VAT area. Compiled rather than fetched, for the same reason
/// <c>InvoiceLabels</c> is: it is small, it never needs to be live, and an invoice must
/// not depend on a network call to state its own tax treatment correctly.
///
/// Not exhaustive — it covers the countries a cross-border biller actually encounters,
/// plus every EU member, because EU membership is what drives reverse charge. Anything
/// absent simply has no code and is treated as unknown rather than guessed at.
/// </summary>
public static class Countries
{
    public sealed record Country(string Code, string Name, bool IsEuVatArea);

    private static readonly Country[] All =
    [
        // EU VAT area — all 27 members. Membership drives reverse charge, so this list
        // being complete and correct matters more than the rest of the table.
        new("AT", "Austria", true),
        new("BE", "Belgium", true),
        new("BG", "Bulgaria", true),
        new("HR", "Croatia", true),
        new("CY", "Cyprus", true),
        new("CZ", "Czechia", true),
        new("DK", "Denmark", true),
        new("EE", "Estonia", true),
        new("FI", "Finland", true),
        new("FR", "France", true),
        new("DE", "Germany", true),
        new("GR", "Greece", true),
        new("HU", "Hungary", true),
        new("IE", "Ireland", true),
        new("IT", "Italy", true),
        new("LV", "Latvia", true),
        new("LT", "Lithuania", true),
        new("LU", "Luxembourg", true),
        new("MT", "Malta", true),
        new("NL", "Netherlands", true),
        new("PL", "Poland", true),
        new("PT", "Portugal", true),
        new("RO", "Romania", true),
        new("SK", "Slovakia", true),
        new("SI", "Slovenia", true),
        new("ES", "Spain", true),
        new("SE", "Sweden", true),

        // Outside the EU VAT area.
        new("GB", "United Kingdom", false),
        new("US", "United States", false),
        new("CA", "Canada", false),
        new("AU", "Australia", false),
        new("NZ", "New Zealand", false),
        new("CH", "Switzerland", false),
        new("NO", "Norway", false),
        new("IS", "Iceland", false),
        new("JP", "Japan", false),
        new("CN", "China", false),
        new("HK", "Hong Kong", false),
        new("SG", "Singapore", false),
        new("KR", "South Korea", false),
        new("TW", "Taiwan", false),
        new("IN", "India", false),
        new("VN", "Vietnam", false),
        new("TH", "Thailand", false),
        new("MY", "Malaysia", false),
        new("ID", "Indonesia", false),
        new("PH", "Philippines", false),
        new("AE", "United Arab Emirates", false),
        new("SA", "Saudi Arabia", false),
        new("QA", "Qatar", false),
        new("KW", "Kuwait", false),
        new("BH", "Bahrain", false),
        new("OM", "Oman", false),
        new("IL", "Israel", false),
        new("TR", "Turkey", false),
        new("ZA", "South Africa", false),
        new("NG", "Nigeria", false),
        new("KE", "Kenya", false),
        new("EG", "Egypt", false),
        new("MA", "Morocco", false),
        new("BR", "Brazil", false),
        new("MX", "Mexico", false),
        new("AR", "Argentina", false),
        new("CL", "Chile", false),
        new("CO", "Colombia", false),
        new("PE", "Peru", false),
        new("UA", "Ukraine", false),
        new("RU", "Russia", false),
        new("RS", "Serbia", false),
        new("PK", "Pakistan", false),
        new("BD", "Bangladesh", false),
        new("LK", "Sri Lanka", false),
        new("NP", "Nepal", false),
    ];

    private static readonly Dictionary<string, Country> ByCode =
        All.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);

    /// <summary>Alternative spellings people actually type, mapped to a code.</summary>
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["uk"] = "GB",
        ["u.k."] = "GB",
        ["great britain"] = "GB",
        ["england"] = "GB",
        ["scotland"] = "GB",
        ["wales"] = "GB",
        ["northern ireland"] = "GB",
        ["britain"] = "GB",
        ["united kingdom of great britain and northern ireland"] = "GB",
        ["usa"] = "US",
        ["u.s."] = "US",
        ["u.s.a."] = "US",
        ["america"] = "US",
        ["united states of america"] = "US",
        ["uae"] = "AE",
        ["holland"] = "NL",
        ["the netherlands"] = "NL",
        ["czech republic"] = "CZ",
        ["south korea"] = "KR",
        ["republic of korea"] = "KR",
        ["korea"] = "KR",
        ["viet nam"] = "VN",
        ["russian federation"] = "RU",
        ["eire"] = "IE",
        ["republic of ireland"] = "IE",
        ["deutschland"] = "DE",
        ["españa"] = "ES",
        ["espana"] = "ES",
    };

    public static IReadOnlyList<Country> AllCountries => All;

    public static Country? ByCodeOrNull(string? code)
        => code is not null && ByCode.TryGetValue(code, out var c) ? c : null;

    public static string? NameOf(string? code) => ByCodeOrNull(code)?.Name;

    public static bool IsEuVatArea(string? code) => ByCodeOrNull(code)?.IsEuVatArea ?? false;

    /// <summary>
    /// Best-effort read of free text into a code — for migrating addresses typed before
    /// there was a code to store. Deliberately conservative: anything it is not sure
    /// about returns null, because a wrong country silently produces a wrong tax
    /// treatment, which is worse than an empty field somebody is asked to fill in.
    /// </summary>
    public static string? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var trimmed = text.Trim().Trim('.', ',').Trim();

        // Already a code.
        if (trimmed.Length == 2 && ByCode.ContainsKey(trimmed))
            return ByCode[trimmed].Code;

        if (Aliases.TryGetValue(trimmed, out var aliased))
            return ByCodeOrNull(aliased)?.Code;

        var byName = All.FirstOrDefault(c => string.Equals(c.Name, trimmed, StringComparison.OrdinalIgnoreCase));
        return byName?.Code;
    }
}
