using System.Globalization;
using System.Text;

namespace Akinai.Domain.Services;

/// <summary>
/// Checks that the payment details on an invoice could actually be paid, before it is sent.
///
/// The failure this exists to prevent is specific and expensive: a wire that is accepted
/// by the sender's bank, travels for several days, and is then returned — with fees taken
/// out of the principal at each hop — because an IBAN digit was wrong or the destination
/// country required a purpose-of-payment code that was not supplied. The client has by
/// then done everything right, and the money is gone for a fortnight.
///
/// Every rule here is arithmetic or a lookup. No network calls: this must run on the
/// send path without being able to fail or stall.
/// </summary>
public static class PayabilityCheck
{
    public enum Severity
    {
        /// <summary>The payment will very likely fail or be returned.</summary>
        Blocking,
        /// <summary>The payment may be delayed or queried, but can succeed.</summary>
        Warning,
    }

    /// <param name="Field">The settings field at fault, so the UI can point at it.</param>
    public sealed record Finding(Severity Severity, string Field, string Message);

    /// <summary>The payment details as stored, in the shape this check needs.</summary>
    public sealed record Details(
        string? Iban = null,
        string? Swift = null,
        string? AccountNumber = null,
        string? SortCode = null,
        string? PurposeCode = null,
        string? IntermediarySwift = null);

    /// <summary>
    /// Destinations whose banks require a stated purpose of payment on inbound
    /// cross-border credits. Sending without one is the most common cause of a payment
    /// sitting in a compliance queue rather than being rejected outright — which is worse,
    /// because nobody is told.
    /// </summary>
    private static readonly Dictionary<string, string> PurposeCodeRequired = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CN"] = "China",
        ["IN"] = "India",
        ["VN"] = "Vietnam",
        ["AE"] = "United Arab Emirates",
        ["SA"] = "Saudi Arabia",
        ["JO"] = "Jordan",
        ["BH"] = "Bahrain",
        ["KW"] = "Kuwait",
        ["OM"] = "Oman",
        ["QA"] = "Qatar",
        ["RU"] = "Russia",
        ["BD"] = "Bangladesh",
        ["PK"] = "Pakistan",
        ["LK"] = "Sri Lanka",
        ["NG"] = "Nigeria",
        ["EG"] = "Egypt",
    };

    /// <summary>
    /// Runs every applicable rule. <paramref name="destinationCountryCode"/> is the
    /// ISO-3166 alpha-2 code of the country the money is being sent to, or null when it
    /// is not known yet — the country-specific rules are then skipped rather than guessed.
    /// </summary>
    public static IReadOnlyList<Finding> Run(Details details, string? destinationCountryCode = null)
    {
        var findings = new List<Finding>();

        var hasIban = !string.IsNullOrWhiteSpace(details.Iban);
        var hasLocal = !string.IsNullOrWhiteSpace(details.AccountNumber);

        if (!hasIban && !hasLocal)
        {
            findings.Add(new Finding(Severity.Blocking, nameof(Details.Iban),
                "No IBAN and no account number — there is nothing for the client to pay into."));
        }

        if (hasIban && !IsValidIban(details.Iban!))
        {
            // Mod-97 catches single-digit typos and transposed pairs, which is the bulk of
            // what people actually get wrong when copying an IBAN by hand.
            findings.Add(new Finding(Severity.Blocking, nameof(Details.Iban),
                "The IBAN fails its checksum, so it is mistyped. A payment to it will be rejected or, worse, credited to somebody else."));
        }

        if (!string.IsNullOrWhiteSpace(details.Swift) && !IsValidBic(details.Swift!))
        {
            findings.Add(new Finding(Severity.Blocking, nameof(Details.Swift),
                "The SWIFT/BIC is not a valid format. It must be 8 or 11 characters: 4 letters, a 2-letter country, then 2 alphanumeric, optionally a 3-character branch."));
        }

        if (!string.IsNullOrWhiteSpace(details.IntermediarySwift) && !IsValidBic(details.IntermediarySwift!))
        {
            findings.Add(new Finding(Severity.Blocking, nameof(Details.IntermediarySwift),
                "The intermediary bank's SWIFT/BIC is not a valid format."));
        }

        // A cross-border wire with no BIC has to be routed by name, which banks increasingly
        // refuse to do.
        if (hasIban && string.IsNullOrWhiteSpace(details.Swift))
        {
            findings.Add(new Finding(Severity.Warning, nameof(Details.Swift),
                "No SWIFT/BIC. Most banks will not route an international payment without one."));
        }

        if (destinationCountryCode is not null
            && PurposeCodeRequired.TryGetValue(destinationCountryCode, out var country)
            && string.IsNullOrWhiteSpace(details.PurposeCode))
        {
            findings.Add(new Finding(Severity.Warning, nameof(Details.PurposeCode),
                $"Banks in {country} require a stated purpose of payment on inbound transfers. Without one the payment can sit in a compliance queue for days with no notification."));
        }

        return findings;
    }

    /// <summary>
    /// ISO 13616 check: move the first four characters to the end, convert letters to
    /// digits (A=10 … Z=35), and the resulting number must be congruent to 1 mod 97.
    /// </summary>
    public static bool IsValidIban(string iban)
    {
        var trimmed = new string(iban.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();

        // 15 (Norway) to 34 (Malta and others) is the full legal range.
        if (trimmed.Length is < 15 or > 34) return false;
        if (!trimmed.All(char.IsLetterOrDigit)) return false;
        if (!char.IsLetter(trimmed[0]) || !char.IsLetter(trimmed[1])) return false;
        if (!char.IsDigit(trimmed[2]) || !char.IsDigit(trimmed[3])) return false;

        var rearranged = trimmed[4..] + trimmed[..4];

        var digits = new StringBuilder(rearranged.Length * 2);
        foreach (var c in rearranged)
        {
            if (char.IsDigit(c)) digits.Append(c);
            else digits.Append((c - 'A' + 10).ToString(CultureInfo.InvariantCulture));
        }

        // Taken 9 digits at a time so this never needs a big-integer type.
        var remainder = 0;
        foreach (var c in digits.ToString())
            remainder = (remainder * 10 + (c - '0')) % 97;

        return remainder == 1;
    }

    /// <summary>ISO 9362: 4 letters bank, 2 letters country, 2 alphanumeric location, optional 3-character branch.</summary>
    public static bool IsValidBic(string bic)
    {
        var trimmed = new string(bic.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();

        if (trimmed.Length is not (8 or 11)) return false;
        if (!trimmed.Take(4).All(char.IsLetter)) return false;
        if (!trimmed.Skip(4).Take(2).All(char.IsLetter)) return false;
        if (!trimmed.Skip(6).Take(2).All(char.IsLetterOrDigit)) return false;
        if (trimmed.Length == 11 && !trimmed.Skip(8).All(char.IsLetterOrDigit)) return false;

        return true;
    }

    /// <summary>True when the destination country's banks expect a purpose-of-payment code.</summary>
    public static bool RequiresPurposeCode(string? countryCode)
        => countryCode is not null && PurposeCodeRequired.ContainsKey(countryCode);
}
