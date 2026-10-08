using System.Text;
using System.Text.RegularExpressions;
using Akinai.Shared.Geography;

namespace Akinai.Shared.Compliance;

/// <summary>
/// Whether a VAT or tax registration number is the right <i>shape</i> for the country it
/// claims to belong to — and, where the scheme publishes a check digit we trust, whether
/// it adds up.
///
/// <para>
/// <b>Why this exists at all.</b> <see cref="CorridorRules"/> can tell you that an EU
/// reverse-charge invoice must carry the client's VAT number, but not whether the number
/// somebody typed into the client form is a number at all. The commonest real failure is
/// not a missing number, it is a transposed digit or a number pasted into the wrong
/// client — and that only surfaces weeks later when the client's accountant rejects the
/// invoice. Catching it at the keystroke is worth a great deal.
/// </para>
///
/// <para>
/// <b>Why offline.</b> VIES (the EU's VAT number service) is the only thing that can tell
/// you a number is really <i>registered</i>, and nothing here pretends otherwise. But VIES
/// is slow, rate-limited, frequently down for individual member states, and useless on a
/// form the user is still typing into. Shape checking is instant, free and never wrong
/// about the thing it actually checks, so it belongs in front of the network call rather
/// than instead of it. No HTTP, no EF, no schema — this is a pure function over a string.
/// </para>
///
/// <para>
/// <b>The governing bias: a false rejection is worse than a missed error.</b> If this
/// refuses a customer's genuine VAT number, the user cannot invoice them, cannot tell who
/// is wrong, and loses trust in every other check the app makes. A number that slips
/// through merely fails later in exactly the way it would have anyway. So every rule here
/// errs loose: where a published pattern is ambiguous the looser reading wins, and where
/// the check-digit algorithm is not one we have verified against known-good numbers we
/// check structure only and say so. <b>No checksum in this file is inferred or invented.</b>
/// Each of the three that exist is noted with the published numbers it was verified
/// against; see <see cref="Rules"/>.
/// </para>
/// </summary>
public static class TaxNumberFormat
{
    /// <summary>
    /// Deliberately three-valued. "We have no rule for this country" is a different
    /// statement from "this is wrong", and a form that renders them the same way teaches
    /// the user to ignore it — a US client has no VAT number to get wrong, and saying so
    /// is honest where a red border would be a lie.
    /// </summary>
    public enum Outcome
    {
        /// <summary>Matches the country's published shape, and passed its checksum if we run one.</summary>
        Valid,

        /// <summary>We have a rule for this country and the number does not satisfy it.</summary>
        WrongShape,

        /// <summary>No rule for this country (or nothing to check). Not an opinion either way.</summary>
        NotChecked,
    }

    /// <param name="Normalised">
    /// The number in canonical prefixed form, set only when it is valid. Worth storing in
    /// preference to what the user typed, so that the same registration entered three
    /// ways compares equal later.
    /// </param>
    /// <param name="ExpectedShape">
    /// Plain-language description of the shape, including an example, so the UI can say
    /// what good looks like instead of only that this is bad. Present whenever we have a
    /// rule — including on success, where a form may want it as a hint.
    /// </param>
    /// <param name="ChecksumVerified">
    /// True only where a real published check digit was computed and passed. Lets the UI
    /// distinguish "this is a well-formed number" from "this is definitely a real number
    /// shape" without overclaiming on the twenty-odd countries we shape-check only.
    /// </param>
    public sealed record Result(
        Outcome Outcome,
        string? Normalised,
        string? ExpectedShape,
        bool ChecksumVerified,
        string Message)
    {
        public bool IsValid => Outcome is TaxNumberFormat.Outcome.Valid;

        /// <summary>
        /// True only for a positive rejection. Note that this is <i>not</i>
        /// <c>!IsValid</c>: an unchecked country is neither valid nor rejected, and UI
        /// that treats the two alike will flag perfectly good US and Canadian clients.
        /// </summary>
        public bool IsRejected => Outcome is TaxNumberFormat.Outcome.WrongShape;
    }

    /// <summary>
    /// One country's rule. <paramref name="Patterns"/> is a list rather than one regex
    /// because several schemes legitimately issue more than one length or layout (UK
    /// branch traders, Irish numbers before and after 2013), and folding those into a
    /// single expression makes it unreadable and therefore unmaintainable.
    /// </summary>
    /// <param name="Prefix">
    /// The VAT prefix, which is not always the ISO country code — Greece files under EL.
    /// Kept separate for exactly that reason.
    /// </param>
    /// <param name="Checksum">
    /// Null means structure-only, and that is the normal case. Receives the body with the
    /// prefix already stripped.
    /// </param>
    private sealed record Rule(
        string Prefix,
        string Shape,
        Regex[] Patterns,
        Func<string, bool>? Checksum = null);

    private static Regex P(string pattern) => new(pattern, RegexOptions.CultureInvariant);

    /// <summary>
    /// Keyed by ISO alpha-2 code, so it joins to <see cref="Countries"/> rather than
    /// restating it. The country list, EU membership and country names all stay there;
    /// this table holds only the one fact it owns, which is the number's layout. Anything
    /// absent from here is <see cref="Outcome.NotChecked"/> by construction — adding a
    /// country is adding a row, and never editing a branch.
    ///
    /// <para>
    /// <b>Checksums.</b> Only three entries carry one, each verified by hand against
    /// published numbers before being trusted:
    /// </para>
    /// <list type="bullet">
    /// <item><b>GB</b> — HMRC's mod-97 rule, both generations. Verified against GB980780684 and GB727255821.</item>
    /// <item><b>AU</b> — the ATO's weighted modulus-89 ABN check. Verified against 51 824 753 556 (the ATO's own) and 53 004 085 616.</item>
    /// <item><b>DE</b> — ISO 7064 MOD 11,10. Verified against DE136695976 and DE123456788.</item>
    /// </list>
    /// <para>
    /// Every other country is <b>structure only, deliberately</b>. Most do publish a check
    /// digit, and for several of them (France's mod-97 key over the SIREN, the Dutch
    /// nine-digit mod-11, Poland's weighted NIP sum) the algorithm is a matter of public
    /// record. They are still not applied here, for two different reasons worth keeping
    /// apart:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <b>Known exceptions inside the issued range.</b> The Netherlands is the clear case:
    /// the BTW-identificatienummer issued to sole traders from 2020 is not derived from
    /// the old nine-digit mod-11 relation, so applying that check would reject a large
    /// population of real, current registrations. That is the precise failure this class
    /// is written to avoid.
    /// </item>
    /// <item>
    /// <b>Unverified coverage.</b> For the rest we can recite an algorithm but have not
    /// confirmed it holds across the whole issued range, including legacy numbers, letter
    /// keys and the dependencies and overseas territories that file under a member state's
    /// prefix. An algorithm that is right about the specimens and wrong about the long
    /// tail is worse than no algorithm, because it fails confidently.
    /// </item>
    /// </list>
    /// <para>
    /// The remedy for both is the same and it is not guesswork: a VIES or HMRC lookup,
    /// which answers the question these checksums only gesture at. When one is added,
    /// this is the gate it belongs behind.
    /// </para>
    /// </summary>
    private static readonly Dictionary<string, Rule> Rules = new(StringComparer.OrdinalIgnoreCase)
    {
        // ---- EU VAT area, all 27. Structure only unless noted. ----

        // The U is part of the number, not of the prefix — every Austrian VAT number
        // carries it, and it is the only letter in one.
        ["AT"] = new("AT", "AT followed by U and 8 digits, for example ATU12345675",
            [P(@"^U\d{8}$")]),

        // Ten digits since 2008, and the first is 0 or 1. A legacy nine-digit number is
        // still valid once it gains the leading zero it was always notionally missing,
        // which is why the shape text says so rather than our quietly accepting nine.
        ["BE"] = new("BE", "BE followed by 10 digits beginning 0 or 1, for example BE0123456749 — a legacy 9-digit number takes a leading 0",
            [P(@"^[01]\d{9}$")]),

        ["BG"] = new("BG", "BG followed by 9 or 10 digits, for example BG123456789",
            [P(@"^\d{9,10}$")]),

        ["CY"] = new("CY", "CY followed by 8 digits and one letter, for example CY12345678L",
            [P(@"^\d{8}[A-Z]$")]),

        ["CZ"] = new("CZ", "CZ followed by 8, 9 or 10 digits, for example CZ12345679",
            [P(@"^\d{8,10}$")]),

        // Checksum: ISO 7064 MOD 11,10. See the remarks above.
        ["DE"] = new("DE", "DE followed by 9 digits, for example DE123456788",
            [P(@"^\d{9}$")], GermanMod1110),

        ["DK"] = new("DK", "DK followed by 8 digits, for example DK12345674",
            [P(@"^\d{8}$")]),

        ["EE"] = new("EE", "EE followed by 9 digits, for example EE123456780",
            [P(@"^\d{9}$")]),

        // Greece is the one country whose VAT prefix is not its ISO code: EL, from the
        // Greek alphabet's own name for the language. Both are accepted on input.
        ["GR"] = new("EL", "EL followed by 9 digits, for example EL123456783",
            [P(@"^\d{9}$")]),

        // Nine characters, at least one of which is a letter: a letter leads for a company
        // (the entity-type code), trails for a foreign national, or both for some entities.
        ["ES"] = new("ES", "ES followed by 9 characters with at least one letter, for example ESB12345678",
            [P(@"^[A-Z]\d{7}[A-Z0-9]$"), P(@"^\d{8}[A-Z]$")]),

        ["FI"] = new("FI", "FI followed by 8 digits, for example FI12345671",
            [P(@"^\d{8}$")]),

        // Two key characters then the nine-digit SIREN. The key is numeric on older
        // registrations and may contain letters on newer ones, so both are admitted.
        ["FR"] = new("FR", "FR followed by a 2-character key and 9 digits, for example FR40303265045",
            [P(@"^[A-Z0-9]{2}\d{9}$")]),

        ["HR"] = new("HR", "HR followed by 11 digits, for example HR12345678903",
            [P(@"^\d{11}$")]),

        ["HU"] = new("HU", "HU followed by 8 digits, for example HU12345676",
            [P(@"^\d{8}$")]),

        // Three layouts in circulation: seven digits and a letter; the same with a second
        // letter (issued since 2013); and the old form with a letter, + or * in second
        // position. The trailing letter is formally restricted to a subrange, but we
        // accept any letter — a wrongly narrowed range here rejects real numbers, and the
        // range is not worth the risk for what it buys.
        ["IE"] = new("IE", "IE followed by 7 digits and 1 or 2 letters, for example IE6388047V",
            [P(@"^\d{7}[A-Z]$"), P(@"^\d{7}[A-Z]{2}$"), P(@"^\d[A-Z+*]\d{5}[A-Z]$")]),

        ["IT"] = new("IT", "IT followed by 11 digits, for example IT12345678903",
            [P(@"^\d{11}$")]),

        ["LT"] = new("LT", "LT followed by 9 or 12 digits, for example LT123456715",
            [P(@"^\d{9}$"), P(@"^\d{12}$")]),

        ["LU"] = new("LU", "LU followed by 8 digits, for example LU12345613",
            [P(@"^\d{8}$")]),

        ["LV"] = new("LV", "LV followed by 11 digits, for example LV12345678901",
            [P(@"^\d{11}$")]),

        ["MT"] = new("MT", "MT followed by 8 digits, for example MT12345634",
            [P(@"^\d{8}$")]),

        // Twelve characters ending in B and two digits — the B separates the nine-digit
        // identifier from the two-digit sub-number, and is always literally B.
        ["NL"] = new("NL", "NL followed by 9 digits, then B, then 2 digits, for example NL004495445B01",
            [P(@"^\d{9}B\d{2}$")]),

        ["PL"] = new("PL", "PL followed by 10 digits, for example PL1234567883",
            [P(@"^\d{10}$")]),

        ["PT"] = new("PT", "PT followed by 9 digits, for example PT123456789",
            [P(@"^\d{9}$")]),

        // Romania's is genuinely variable-length and short numbers are real, not truncated.
        ["RO"] = new("RO", "RO followed by between 2 and 10 digits, for example RO1234567897",
            [P(@"^\d{2,10}$")]),

        ["SE"] = new("SE", "SE followed by 12 digits, for example SE123456789701",
            [P(@"^\d{12}$")]),

        ["SI"] = new("SI", "SI followed by 8 digits, for example SI12345679",
            [P(@"^\d{8}$")]),

        ["SK"] = new("SK", "SK followed by 10 digits, for example SK1234567890",
            [P(@"^\d{10}$")]),

        // ---- Outside the EU VAT area. ----

        // Nine digits normally; twelve for a branch trader, where the last three identify
        // the branch and carry no check of their own. GD and HA are the government
        // department and health authority forms — they are real VAT numbers that appear on
        // real invoices, they have no check digits, and a validator that rejects them
        // locks public-sector clients out of the app.
        ["GB"] = new("GB",
            "GB followed by 9 digits (or 12 for a branch trader), or GD plus 3 digits for a government department, or HA plus 3 for a health authority — for example GB980780684",
            [P(@"^\d{9}$"), P(@"^\d{12}$"), P(@"^GD[0-4]\d{2}$"), P(@"^HA[5-9]\d{2}$")],
            UkMod97),

        // The ABN. Australia has no VAT prefix on the number itself, but people paste it
        // with one anyway, so AU is accepted and stripped like any other.
        ["AU"] = new("AU", "11 digits, for example 51 824 753 556",
            [P(@"^\d{11}$")], AbnMod89),
    };

    /// <summary>
    /// Reverse index from VAT prefix to ISO code, used only to recognise a number that
    /// belongs to some <i>other</i> country. Pasting the wrong client's VAT number is a
    /// commoner mistake than mistyping one, and "that looks like a French number" is a far
    /// more useful thing to say than "wrong shape".
    /// </summary>
    private static readonly Dictionary<string, string> IsoByPrefix =
        Rules.ToDictionary(r => r.Value.Prefix, r => r.Key, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Reduces what a human typed to the characters that carry meaning. People write VAT
    /// numbers with spaces, dots, slashes and dashes in whatever grouping their own
    /// country uses, and all of it is presentation: rather than enumerate the separators
    /// we keep only the signal. Non-ASCII goes too, which conveniently means the patterns
    /// above cannot be fooled by a Unicode digit look-alike.
    ///
    /// <para>
    /// <c>+</c> and <c>*</c> survive, and are the reason this is not simply
    /// <c>IsAsciiLetterOrDigit</c>. They are legitimate characters inside an older Irish
    /// VAT number, where they occupy the second position in place of a letter. They look
    /// like punctuation and are not, so stripping them would quietly make every such
    /// number unvalidatable.
    /// </para>
    /// </summary>
    public static string Normalise(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
            if (char.IsAsciiLetterOrDigit(c) || c is '+' or '*')
                sb.Append(char.ToUpperInvariant(c));

        return sb.ToString();
    }

    /// <summary>Whether we have anything to say about this country's numbers at all.</summary>
    public static bool HasRuleFor(string? countryCode)
        => countryCode is not null && Rules.ContainsKey(countryCode);

    /// <summary>
    /// The shape description on its own, for a form that wants a placeholder or hint
    /// before the user has typed anything there is to validate.
    /// </summary>
    public static string? ExpectedShapeFor(string? countryCode)
        => countryCode is not null && Rules.TryGetValue(countryCode, out var rule) ? rule.Shape : null;

    /// <summary>
    /// The VAT prefix for a country, where it differs from the ISO code — Greece's EL
    /// being the only case in this table, and the reason this is a method and not an
    /// assumption spread through the UI.
    /// </summary>
    public static string? PrefixFor(string? countryCode)
        => countryCode is not null && Rules.TryGetValue(countryCode, out var rule) ? rule.Prefix : null;

    /// <summary>
    /// Checks <paramref name="number"/> against <paramref name="countryCode"/> (ISO
    /// alpha-2, as stored on the client and business profile).
    ///
    /// <para>
    /// The prefix is optional on input and both conventions are common in the wild, so
    /// "DE123456788", "123456788" and "de 123.456.788" are the same number. Greece accepts
    /// its ISO code and its VAT prefix alike.
    /// </para>
    /// </summary>
    public static Result Validate(string? number, string? countryCode)
        => Validate(number, countryCode, suggestOtherCountries: true);

    private static Result Validate(string? number, string? countryCode, bool suggestOtherCountries)
    {
        if (string.IsNullOrWhiteSpace(countryCode) || !Rules.TryGetValue(countryCode, out var rule))
        {
            // Deliberately not a rejection. Most of the world has no VAT number with a
            // checkable shape, and a US or Canadian client with an EIN or BN typed into
            // this field is not making a mistake we are entitled to flag.
            return new Result(Outcome.NotChecked, null, null, false,
                Countries.NameOf(countryCode) is { } named
                    ? $"We have no number format on file for {named}, so this was not checked."
                    : string.IsNullOrWhiteSpace(countryCode)
                        ? "No country is set, so this number was not checked."
                        : "That country is not recognised, so this number was not checked.");
        }

        var normalised = Normalise(number);
        if (normalised.Length == 0)
        {
            // Whether a missing number *matters* is a corridor question, not a format one,
            // and CorridorRules already answers it. Saying nothing here avoids two
            // components shouting about the same empty field.
            return new Result(Outcome.NotChecked, null, rule.Shape, false,
                "There is no number to check.");
        }

        var prefix = rule.Prefix;
        foreach (var body in Candidates(normalised, prefix, countryCode))
        {
            if (!rule.Patterns.Any(p => p.IsMatch(body))) continue;

            if (rule.Checksum is null)
                return new Result(Outcome.Valid, prefix + body, rule.Shape, false,
                    "The format is right for this country. Whether the number is registered can only be confirmed by a lookup.");

            if (rule.Checksum(body))
                return new Result(Outcome.Valid, prefix + body, rule.Shape, true,
                    "The format and check digits are both right. Whether the number is registered can only be confirmed by a lookup.");

            // Right shape, wrong arithmetic — which in practice means a transposed or
            // mistyped digit. Reported as a rejection because the remedy is identical
            // (look at it again), but the message says which half failed so the user is
            // not left hunting for a length error that is not there.
            return new Result(Outcome.WrongShape, null, rule.Shape, false,
                $"That is the right length for {Countries.NameOf(countryCode)}, but its check digits do not add up — usually a mistyped or transposed digit.");
        }

        return new Result(Outcome.WrongShape, null, rule.Shape, false,
            $"That does not look like a {Countries.NameOf(countryCode)} number. Expected {rule.Shape}."
            + (suggestOtherCountries ? OtherCountryHint(normalised, countryCode) : string.Empty));
    }

    /// <summary>
    /// The forms of the input worth trying: as given, and with the country's own prefix
    /// removed. Both are tried rather than stripping unconditionally because stripping is
    /// a guess — two leading letters might be a prefix or might be the number's own first
    /// characters, and only the pattern knows which. Trying both costs a regex and removes
    /// the guess entirely.
    /// </summary>
    private static IEnumerable<string> Candidates(string normalised, string prefix, string isoCode)
    {
        yield return normalised;

        if (normalised.Length > 2 && normalised.StartsWith(prefix, StringComparison.Ordinal))
            yield return normalised[2..];

        // Greece again: the number reads EL123456783 but the client record says GR, and
        // somebody will type the code they can see on screen.
        if (!prefix.Equals(isoCode, StringComparison.OrdinalIgnoreCase)
            && normalised.Length > 2
            && normalised.StartsWith(isoCode, StringComparison.OrdinalIgnoreCase))
        {
            yield return normalised[2..];
        }
    }

    /// <summary>
    /// If the number is a perfectly good number for a different country, say whose.
    /// Recursion is bounded by passing <c>suggestOtherCountries: false</c> — the hint
    /// never needs a hint of its own.
    /// </summary>
    private static string OtherCountryHint(string normalised, string thisCountry)
    {
        if (normalised.Length < 3) return string.Empty;
        if (!IsoByPrefix.TryGetValue(normalised[..2], out var otherIso)) return string.Empty;
        if (otherIso.Equals(thisCountry, StringComparison.OrdinalIgnoreCase)) return string.Empty;
        if (!Validate(normalised, otherIso, suggestOtherCountries: false).IsValid) return string.Empty;

        return $" It is a valid {Countries.NameOf(otherIso)} number — check you have the right client, or the right country on this one.";
    }

    // ------------------------------------------------------------------
    // Checksums. Three, each verified against published numbers.
    // ------------------------------------------------------------------

    /// <summary>
    /// HMRC's mod-97 rule. Weight the first seven digits by 8…2, add the two-digit check,
    /// and the total must be divisible by 97.
    ///
    /// <para>
    /// There are <b>two</b> live generations of this rule: numbers from the original
    /// series satisfy it directly, and numbers issued once that series was exhausted
    /// satisfy it with 55 added. Both are genuine, currently valid VAT numbers, so both
    /// are accepted — checking only the older relation would reject roughly every number
    /// issued this century, which is the exact failure mode this class exists to avoid.
    /// </para>
    ///
    /// <para>
    /// A twelve-digit branch number is checked on its first nine: the trailing three
    /// identify the branch and have no check digits. GD and HA numbers have no check
    /// digits either, so there is nothing to verify and nothing to reject.
    /// </para>
    /// </summary>
    private static bool UkMod97(string body)
    {
        if (body.StartsWith("GD", StringComparison.Ordinal) || body.StartsWith("HA", StringComparison.Ordinal))
            return true;

        var nine = body.Length == 12 ? body[..9] : body;
        if (nine.Length != 9) return false;

        int[] weights = [8, 7, 6, 5, 4, 3, 2];
        var total = 0;
        for (var i = 0; i < 7; i++) total += (nine[i] - '0') * weights[i];

        var check = ((nine[7] - '0') * 10) + (nine[8] - '0');

        return (total + check) % 97 == 0 || (total + check + 55) % 97 == 0;
    }

    /// <summary>
    /// The ATO's ABN check: subtract one from the first digit, weight all eleven by
    /// 10, 1, 3, 5, …, 19, and the total must be divisible by 89.
    ///
    /// <para>
    /// The leading subtraction is the part people drop when reimplementing this from
    /// memory, and dropping it rejects every ABN beginning with a digit other than one.
    /// </para>
    /// </summary>
    private static bool AbnMod89(string body)
    {
        if (body.Length != 11) return false;

        int[] weights = [10, 1, 3, 5, 7, 9, 11, 13, 15, 17, 19];
        var total = 0;
        for (var i = 0; i < 11; i++)
        {
            var digit = body[i] - '0';
            if (i == 0) digit -= 1;
            total += digit * weights[i];
        }

        return total % 89 == 0;
    }

    /// <summary>
    /// The German USt-IdNr check digit: ISO 7064 MOD 11,10 over the first eight digits.
    ///
    /// <para>
    /// Included because, unusually, it is both unambiguous and uniform — Germany has
    /// issued one series under one algorithm, with none of the legacy forms or letter keys
    /// that make the other member states' checks a gamble. Verified against DE136695976
    /// and DE123456788 before being trusted.
    /// </para>
    /// </summary>
    private static bool GermanMod1110(string body)
    {
        if (body.Length != 9) return false;

        var product = 10;
        for (var i = 0; i < 8; i++)
        {
            var sum = ((body[i] - '0') + product) % 10;
            if (sum == 0) sum = 10;
            product = (2 * sum) % 11;
        }

        var check = 11 - product;
        if (check == 10) check = 0;

        return check == body[8] - '0';
    }
}
