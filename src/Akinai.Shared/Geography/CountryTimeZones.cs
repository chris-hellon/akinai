namespace Akinai.Shared.Geography;

/// <summary>
/// A rough UTC offset for a country, used to land a reminder inside the client's working
/// day rather than the sender's.
///
/// <para>
/// <b>Deliberately approximate, and only where approximation is honest.</b> Countries that
/// genuinely span several zones — the United States, Canada, Australia, Russia, Brazil —
/// are absent rather than guessed at, because picking one of their zones would be wrong
/// for most of the country. An absent country falls back to UTC, which is no worse than
/// having no notion of the client's day at all.
/// </para>
///
/// <para>
/// Offsets ignore daylight saving. An hour's drift at the edges of the year moves a
/// reminder within the working day, not outside it, which is the only precision this
/// needs. Storing a real IANA zone per client would be better and is the obvious upgrade
/// if anyone ever complains about the timing.
/// </para>
/// </summary>
public static class CountryTimeZones
{
    private static readonly Dictionary<string, int> OffsetHours = new(StringComparer.OrdinalIgnoreCase)
    {
        // Western Europe
        ["GB"] = 0, ["IE"] = 0, ["PT"] = 0, ["IS"] = 0,
        // Central Europe
        ["FR"] = 1, ["DE"] = 1, ["NL"] = 1, ["BE"] = 1, ["LU"] = 1, ["ES"] = 1,
        ["IT"] = 1, ["AT"] = 1, ["CH"] = 1, ["PL"] = 1, ["CZ"] = 1, ["SK"] = 1,
        ["HU"] = 1, ["SI"] = 1, ["HR"] = 1, ["DK"] = 1, ["SE"] = 1, ["NO"] = 1,
        ["RS"] = 1, ["MT"] = 1,
        // Eastern Europe
        ["FI"] = 2, ["EE"] = 2, ["LV"] = 2, ["LT"] = 2, ["RO"] = 2, ["BG"] = 2,
        ["GR"] = 2, ["CY"] = 2, ["UA"] = 2, ["IL"] = 2, ["EG"] = 2, ["ZA"] = 2,
        // Middle East and Africa
        ["TR"] = 3, ["SA"] = 3, ["QA"] = 3, ["BH"] = 3, ["KW"] = 3, ["KE"] = 3,
        ["AE"] = 4, ["OM"] = 4,
        ["PK"] = 5,
        ["LK"] = 5, ["IN"] = 5,          // both :30 in reality; see Offset below
        ["NP"] = 5,
        ["BD"] = 6,
        // South-east and east Asia
        ["TH"] = 7, ["VN"] = 7, ["ID"] = 7,
        ["CN"] = 8, ["HK"] = 8, ["SG"] = 8, ["MY"] = 8, ["TW"] = 8, ["PH"] = 8,
        ["JP"] = 9, ["KR"] = 9,
        ["NZ"] = 12,
        // Latin America, single-zone only
        ["CO"] = -5, ["PE"] = -5, ["CL"] = -4, ["AR"] = -3,
        ["MA"] = 1, ["NG"] = 1,
    };

    /// <summary>Countries whose offset includes a half hour.</summary>
    private static readonly Dictionary<string, int> ExtraMinutes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["IN"] = 30, ["LK"] = 30, ["NP"] = 45,
    };

    /// <summary>
    /// The client's approximate offset from UTC, or <see cref="TimeSpan.Zero"/> when the
    /// country is unknown or spans several zones.
    /// </summary>
    public static TimeSpan OffsetFor(string? countryCode)
    {
        if (countryCode is null || !OffsetHours.TryGetValue(countryCode, out var hours))
            return TimeSpan.Zero;

        var minutes = ExtraMinutes.TryGetValue(countryCode, out var extra) ? extra : 0;
        return new TimeSpan(hours, minutes, 0);
    }

    /// <summary>True when a real offset is known, rather than the UTC fallback.</summary>
    public static bool IsKnown(string? countryCode)
        => countryCode is not null && OffsetHours.ContainsKey(countryCode);
}
