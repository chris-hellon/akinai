using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace Akinai.Shared.Payments;

/// <summary>
/// The national payment rails whose QR payload formats this file can produce.
///
/// Each rail is a different national scheme with its own encoding; there is no universal
/// payment QR. A client's banking app understands exactly one of these, so which rail is
/// usable is decided entirely by where the money is going, not by preference.
/// </summary>
public enum PaymentRail
{
    /// <summary>European Payments Council "BCD" code for a SEPA Credit Transfer (EPC069-12).</summary>
    Epc,
    /// <summary>India's Unified Payments Interface deep link (<c>upi://pay?…</c>).</summary>
    Upi,
    /// <summary>Brazil's PIX "BR Code" — an EMVCo merchant-presented QR.</summary>
    Pix,
    /// <summary>Thailand's PromptPay — also an EMVCo merchant-presented QR, different template.</summary>
    PromptPay,
}

/// <summary>
/// Everything any of the rails might need, in one shape, so a caller does not have to know
/// which rail it is building for before it collects the data.
/// </summary>
/// <param name="PayeeName">
/// Who is being paid, as it should appear in the payer's banking app. Most rails cap this
/// hard (EPC 70 characters, EMVCo 25) and it is truncated rather than rejected: a shortened
/// name still lets the payer recognise the recipient, whereas no code at all helps nobody.
/// </param>
/// <param name="AccountIdentifier">
/// The thing the money is sent to, in whatever form the rail uses: an IBAN for EPC, a
/// virtual payment address for UPI, a PIX key for PIX, a phone/national ID/e-wallet number
/// for PromptPay. Deliberately one field: the same invoice carries one payment destination,
/// and a rail that cannot make sense of it simply reports itself unavailable.
/// </param>
/// <param name="Amount">
/// The amount to pre-fill, or null to let the payer type it. Null is a legitimate choice on
/// every rail here and is not an error — a code with no amount is still useful, and is what
/// you want for a recurring or partially-paid invoice.
/// </param>
/// <param name="CurrencyCode">
/// ISO 4217 alpha-3. Every rail here is single-currency, so this is really a gate: a EUR
/// invoice cannot be paid through PIX, and a QR that said otherwise would mislead.
/// </param>
/// <param name="Reference">
/// What the payee needs back in order to reconcile the payment, normally the invoice
/// number. Rails carry this very differently — EPC as remittance information, UPI as a
/// transaction note, PIX as a <c>txid</c> — and PromptPay cannot carry it at all.
/// </param>
/// <param name="BeneficiaryBic">
/// The beneficiary bank's BIC. Optional for EPC version 002 inside the EEA, and mandatory
/// for SCT participants outside it, which is why it is part of the request rather than
/// something only the payability check cares about.
/// </param>
/// <param name="PayeeCity">
/// Required by EMVCo (tag 60) and therefore needed for PIX. It is not used for anything
/// else, and a placeholder is substituted rather than failing, because nobody's invoice
/// should lose its payment code over a city name no app displays.
/// </param>
/// <param name="PurposeCode">
/// An ISO 20022 external purpose code (4 characters, e.g. <c>GDDS</c>, <c>SCVE</c>). EPC
/// only. Supplying one occasionally speeds a payment through a bank's screening; supplying
/// a malformed one is worse than supplying none, so anything that is not exactly four
/// alphanumerics is dropped.
/// </param>
public sealed record PaymentQrRequest(
    string? PayeeName = null,
    string? AccountIdentifier = null,
    decimal? Amount = null,
    string? CurrencyCode = null,
    string? Reference = null,
    string? BeneficiaryBic = null,
    string? PayeeCity = null,
    string? PurposeCode = null);

/// <summary>
/// The outcome for one rail: either a payload, or the reason there isn't one.
///
/// There is no third state and no exception. Callers render a QR when
/// <see cref="IsAvailable"/> is true and render nothing otherwise, and the reason exists so
/// that a settings screen can tell the user <em>why</em> ("no IBAN on your profile") rather
/// than silently omitting a feature they expected.
/// </summary>
/// <param name="UnavailableReason">
/// Plain English, addressed to the user, and only populated when <see cref="Payload"/> is
/// null. Never a stack trace and never a spec citation.
/// </param>
public sealed record PaymentQrResult(PaymentRail Rail, string? Payload, string? UnavailableReason)
{
    /// <summary>True when there is a payload safe to encode into a QR code.</summary>
    [MemberNotNullWhen(true, nameof(Payload))]
    public bool IsAvailable => Payload is not null;

    internal static PaymentQrResult Available(PaymentRail rail, string payload) => new(rail, payload, null);

    internal static PaymentQrResult NotAvailable(PaymentRail rail, string reason) => new(rail, null, reason);
}

/// <summary>
/// Builds the text that goes inside a payment QR code, for the national rails our clients
/// actually pay from. It produces strings only — rendering the image is somebody else's job,
/// which is the point: the hard, testable, breakable part of a payment QR is the payload.
///
/// <para>
/// <b>The failure this exists to avoid.</b> A QR code on an invoice is scanned once, by a
/// person who will not scrutinise what their banking app pre-fills. A payload with a
/// mistyped IBAN, a truncated creditor reference or a wrong CRC either fails to scan — mild
/// embarrassment — or scans into a payment that goes to the wrong place or cannot be matched
/// back to the invoice. That is why every method here would rather return
/// <see cref="PaymentQrResult.NotAvailable"/> than emit a payload it is not sure of:
/// <b>no QR code is strictly better than a wrong one.</b> An invoice without a QR is still
/// an invoice; the bank details are printed on it in plain text.
/// </para>
///
/// <para>
/// <b>Why these four.</b> EPC covers every client in the SEPA area and is by far the most
/// used. UPI, PIX and PromptPay are the three places where a QR is not a convenience but the
/// normal way a business pays — in Brazil and Thailand an invoice without one looks broken.
/// </para>
///
/// <para>
/// <b>Pure and deterministic.</b> No clock, no culture, no network, no I/O. Everything is
/// formatted with <see cref="CultureInfo.InvariantCulture"/>, because a payload built on a
/// machine whose locale uses a decimal comma would encode <c>EUR12,34</c> and be rejected or,
/// far worse, read as 1234.
/// </para>
/// </summary>
public static class PaymentQrPayload
{
    /// <summary>Dispatches to the rail-specific builder. See each method for its rules.</summary>
    public static PaymentQrResult Build(PaymentRail rail, PaymentQrRequest request) => rail switch
    {
        PaymentRail.Epc => Epc(request),
        PaymentRail.Upi => Upi(request),
        PaymentRail.Pix => Pix(request),
        PaymentRail.PromptPay => PromptPay(request),
        _ => PaymentQrResult.NotAvailable(rail, "This payment rail is not supported."),
    };

    // ---------------------------------------------------------------------------------
    // EPC — SEPA Credit Transfer (EPC069-12 v3.0)
    // ---------------------------------------------------------------------------------

    /// <summary>The service tag that identifies the payload to a scanner. Fixed by the spec.</summary>
    private const string EpcServiceTag = "BCD";

    /// <summary>
    /// Payload version. 002 is the current one and the reason we use it is the BIC: under
    /// version 001 the BIC is mandatory, and most of our users do not have their bank's BIC
    /// to hand. Version 002 makes it optional for payments that stay inside the EEA, so
    /// emitting 002 is what lets an IBAN-only profile have a working QR code at all.
    /// </summary>
    private const string EpcVersion = "002";

    /// <summary>
    /// Character set 1 = UTF-8. The spec also permits seven ISO 8859 variants, and some of
    /// them encode a payload in fewer bytes, which matters against the 331-byte ceiling. We
    /// do not use them: user-entered names and invoice references here come from anywhere,
    /// and a single character outside the chosen 8859 page would be silently mangled into
    /// the wrong beneficiary name. Spending bytes to never corrupt a name is the right trade.
    /// </summary>
    private const string EpcCharacterSet = "1";

    /// <summary>Identification code; "SCT" is the only value the scheme defines.</summary>
    private const string EpcIdentification = "SCT";

    private const int EpcMaxNameLength = 70;
    private const int EpcMaxStructuredRemittanceLength = 35;
    private const int EpcMaxUnstructuredRemittanceLength = 140;

    /// <summary>
    /// The whole payload must fit a version-13 QR code at error-correction level M, which is
    /// 331 bytes. Exceeding it does not produce a bigger code — it produces one that scheme
    /// members' apps are not obliged to read.
    /// </summary>
    private const int EpcMaxPayloadBytes = 331;

    /// <summary>Scheme floor and ceiling for the pre-filled amount (EPC069-12, AT-04).</summary>
    private const decimal EpcMinAmount = 0.01m;
    private const decimal EpcMaxAmount = 999_999_999.99m;

    /// <summary>
    /// SEPA scheme reach. An EPC code instructs a SEPA Credit Transfer, so an IBAN from a
    /// country the scheme does not reach cannot be paid by one however well-formed the
    /// payload is — the payer's app would accept the scan and the transfer would then fail.
    ///
    /// <para>
    /// <b>Reviewer: this list is a point-in-time snapshot and the scheme's membership
    /// changes.</b> It is EEA plus the non-EEA participants listed in
    /// <see cref="EpcBicRequiredCountries"/>, and should be checked against the EPC's current
    /// list of SEPA scheme countries.
    /// </para>
    /// </summary>
    private static readonly HashSet<string> EpcSepaCountries = new(StringComparer.OrdinalIgnoreCase)
    {
        // EEA: EU 27 …
        "AT", "BE", "BG", "HR", "CY", "CZ", "DK", "EE", "FI", "FR", "DE", "GR", "HU", "IE",
        "IT", "LV", "LT", "LU", "MT", "NL", "PL", "PT", "RO", "SK", "SI", "ES", "SE",
        // … plus the EFTA members of the EEA.
        "IS", "LI", "NO",
        // Non-EEA scheme participants.
        "AD", "CH", "GB", "GI", "GG", "IM", "JE", "MC", "SM", "VA",
        "AL", "MD", "ME", "MK", "RS",
    };

    /// <summary>
    /// Countries where EPC069-12 keeps the BIC mandatory: SCT scheme participants outside the
    /// EEA. "IBAN only" addressing was a consequence of EU Regulation 260/2012, which does not
    /// reach these, so their payments are still routed on the BIC.
    ///
    /// A UK or Swiss IBAN with no BIC therefore gets no EPC code. That is deliberate and
    /// consistent with the payability check, which already warns that most banks will not route
    /// an international payment without a BIC — but here the consequence is a code that scans
    /// into a payment the payer's bank may refuse to send, so it is a refusal rather than a
    /// warning.
    /// </summary>
    private static readonly HashSet<string> EpcBicRequiredCountries = new(StringComparer.OrdinalIgnoreCase)
    {
        "AD", "CH", "GB", "GI", "GG", "IM", "JE", "MC", "SM", "VA",
        "AL", "MD", "ME", "MK", "RS",
    };

    /// <summary>
    /// Builds an EPC ("BCD") payload for a SEPA Credit Transfer, per EPC069-12 v3.0.
    ///
    /// <para>
    /// The payload is up to twelve elements in a fixed order, one per line: service tag,
    /// version, character set, identification, BIC, beneficiary name, IBAN, amount, purpose,
    /// structured remittance, unstructured remittance, beneficiary-to-originator information.
    /// <b>Order is the whole format</b> — there are no tags or keys, so an element in the
    /// wrong position silently becomes a different field, and an extra blank line shifts
    /// everything after it. That is why this builds a fixed-size array and fills positions
    /// rather than appending as it goes.
    /// </para>
    ///
    /// <para>
    /// Two trailing rules matter. The spec says the last populated element is followed by no
    /// separator, so unused trailing elements and <em>their line feeds</em> are dropped
    /// entirely; but an empty element in the <em>middle</em> must still be an empty line,
    /// holding the position of the fields after it. Get either backwards and a reader either
    /// rejects the code or puts the remittance text in the purpose field.
    /// </para>
    ///
    /// <para>
    /// The two remittance elements are mutually exclusive: the spec marks them <c>{Or}</c>, so
    /// at most one may be populated. A reference that is a valid ISO 11649 creditor reference
    /// goes in the structured element, where the payer's bank will carry it through untouched;
    /// anything else goes in the unstructured one as free text.
    /// </para>
    /// </summary>
    public static PaymentQrResult Epc(PaymentQrRequest request)
    {
        const PaymentRail rail = PaymentRail.Epc;

        var name = Sanitise(request.PayeeName);
        if (name.Length == 0)
            return PaymentQrResult.NotAvailable(rail, "Your business name is needed before a SEPA QR code can be produced.");

        var iban = Compact(request.AccountIdentifier);
        if (iban.Length == 0)
            return PaymentQrResult.NotAvailable(rail, "A SEPA QR code needs an IBAN, and none is set.");

        // Checked, not merely present. A mod-97 failure means the IBAN was mistyped, and a
        // mistyped IBAN inside a QR code is the single most expensive thing this file could
        // produce: the payer never reads it, and the money either bounces after a week of
        // fees or lands in a stranger's account.
        if (!IsValidIban(iban))
            return PaymentQrResult.NotAvailable(rail, "The IBAN fails its checksum, so it is mistyped. Fix it before a QR code can be produced.");

        var ibanCountry = iban[..2];
        if (!EpcSepaCountries.Contains(ibanCountry))
            return PaymentQrResult.NotAvailable(rail, "This IBAN is from outside the SEPA area, so it cannot be paid by SEPA credit transfer.");

        var bic = Compact(request.BeneficiaryBic);
        if (bic.Length > 0 && !IsValidBic(bic))
            return PaymentQrResult.NotAvailable(rail, "The SWIFT/BIC is not a valid format, so it cannot be put into a QR code.");

        if (bic.Length == 0 && EpcBicRequiredCountries.Contains(ibanCountry))
            return PaymentQrResult.NotAvailable(rail, "An IBAN from outside the EEA needs a SWIFT/BIC before it can go into a SEPA QR code.");

        // Currency is a gate, not a field: the format has no currency element because it can
        // only ever mean euro.
        if (!IsCurrency(request.CurrencyCode, "EUR"))
            return PaymentQrResult.NotAvailable(rail, "SEPA QR codes can only carry euro amounts.");

        var amountField = string.Empty;
        if (request.Amount is { } amount)
        {
            if (!TryFormatAmount(amount, EpcMinAmount, EpcMaxAmount, out var formatted))
                return PaymentQrResult.NotAvailable(rail, "This amount cannot be put into a SEPA QR code.");

            amountField = "EUR" + formatted;
        }

        // Four alphanumerics or nothing. The spec allows 1..4, but every code actually in the
        // ISO 20022 external purpose list is exactly four, so anything else is a data-entry
        // mistake and a bank screening a payment on a nonsense purpose code is a delay we
        // would have caused.
        var purpose = Sanitise(request.PurposeCode).ToUpperInvariant();
        if (purpose.Length != 4 || !purpose.All(char.IsAsciiLetterOrDigit))
            purpose = string.Empty;

        var reference = Sanitise(request.Reference);
        var structured = string.Empty;
        var unstructured = string.Empty;

        if (reference.Length > 0)
        {
            var candidate = Compact(reference).ToUpperInvariant();
            if (IsValidCreditorReference(candidate))
            {
                // A creditor reference is machine-matched at the far end, so it must survive
                // intact. It is never truncated — if it does not fit, the code is refused
                // further down rather than shortened into a reference that matches nothing.
                structured = candidate[..Math.Min(candidate.Length, EpcMaxStructuredRemittanceLength)];
            }
            else
            {
                unstructured = Truncate(reference, EpcMaxUnstructuredRemittanceLength);
            }
        }

        var payload = ComposeEpc(
            bic, Truncate(name, EpcMaxNameLength), iban, amountField, purpose, structured, unstructured);

        // Shortening the free-text remittance is the only safe way to lose bytes: the name
        // identifies the payee, the reference is matched by machine, and everything else is
        // fixed-length. A long invoice note is the field that can give.
        while (Encoding.UTF8.GetByteCount(payload) > EpcMaxPayloadBytes && unstructured.Length > 0)
        {
            unstructured = unstructured[..^1].TrimEnd();
            payload = ComposeEpc(
                bic, Truncate(name, EpcMaxNameLength), iban, amountField, purpose, structured, unstructured);
        }

        if (Encoding.UTF8.GetByteCount(payload) > EpcMaxPayloadBytes)
            return PaymentQrResult.NotAvailable(rail, "These payment details are too long to fit in a SEPA QR code.");

        return PaymentQrResult.Available(rail, payload);
    }

    /// <summary>
    /// Lays the elements out in spec order and applies the trailing rule. Separated out
    /// because the byte-budget loop above has to rebuild the payload to measure it, and the
    /// two copies drifting apart would be a silent field-order bug.
    /// </summary>
    private static string ComposeEpc(
        string bic, string name, string iban, string amount, string purpose, string structured, string unstructured)
    {
        string[] elements =
        [
            EpcServiceTag,        // 1
            EpcVersion,           // 2
            EpcCharacterSet,      // 3
            EpcIdentification,    // 4
            bic,                  // 5
            name,                 // 6
            iban,                 // 7
            amount,               // 8
            purpose,              // 9
            structured,           // 10  {Or}
            unstructured,         // 11  {Or}
            string.Empty,         // 12  beneficiary to originator information — never set by us
        ];

        var last = Array.FindLastIndex(elements, e => e.Length > 0);

        // Line feed, not CRLF. The spec permits either, and LF is one byte against a 331-byte
        // budget that eleven separators would otherwise cost eleven extra bytes of.
        return string.Join('\n', elements[..(last + 1)]);
    }

    // ---------------------------------------------------------------------------------
    // UPI — India
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// NPCI caps the transaction note at 50 characters. It is the only free text UPI carries,
    /// so a long invoice note is truncated into it rather than dropped.
    /// </summary>
    private const int UpiMaxNoteLength = 50;

    /// <summary>
    /// Builds a UPI deep link: <c>upi://pay?pa=…&amp;pn=…&amp;am=…&amp;cu=INR&amp;tn=…</c>.
    ///
    /// <para>
    /// Unlike the other three rails this is a URI, which makes percent-encoding the thing most
    /// likely to go wrong. Every value is escaped with <see cref="Uri.EscapeDataString"/>,
    /// which encodes a space as <c>%20</c> rather than <c>+</c>. That distinction is not
    /// cosmetic: <c>+</c> only means "space" by the HTML form convention, and a UPI app that
    /// reads the query string as a plain URI will pass a literal plus sign through into the
    /// payee name. An unescaped <c>&amp;</c> in a company name is worse still — it would end
    /// the name and start a parameter the app may or may not recognise.
    /// </para>
    ///
    /// <para>
    /// The reference travels in <c>tn</c> (transaction note), which is what the payer sees and
    /// what shows up on their statement. UPI also defines <c>tr</c> for a machine reference,
    /// but it is specified for PSP-issued identifiers rather than for a merchant's own invoice
    /// number, so it is left alone.
    /// </para>
    /// </summary>
    public static PaymentQrResult Upi(PaymentQrRequest request)
    {
        const PaymentRail rail = PaymentRail.Upi;

        // Sanitised but not compacted. An IBAN is conventionally written in groups of four, so
        // stripping its spaces restores the real value; a UPI ID has no such convention, so a
        // space in one means the user typed something that is not a UPI ID. Quietly removing
        // it would turn "acme name@ybl" into "acmename@ybl" — possibly somebody else's
        // address, and paid without question.
        var vpa = Sanitise(request.AccountIdentifier);
        if (vpa.Length == 0)
            return PaymentQrResult.NotAvailable(rail, "A UPI QR code needs a UPI ID, and none is set.");

        // A virtual payment address is "handle@psp". Anything else — an IBAN, an account
        // number, an email address with a handle a PSP does not serve — produces a link that
        // opens an app and then fails, so it is rejected here instead.
        if (!IsValidVpa(vpa))
            return PaymentQrResult.NotAvailable(rail, "This does not look like a UPI ID. It should be in the form name@bank.");

        var name = Sanitise(request.PayeeName);
        if (name.Length == 0)
            return PaymentQrResult.NotAvailable(rail, "Your business name is needed before a UPI QR code can be produced.");

        // UPI settles in rupees only; there is no currency other than INR to put in cu.
        if (!IsCurrency(request.CurrencyCode, "INR"))
            return PaymentQrResult.NotAvailable(rail, "UPI QR codes can only carry rupee amounts.");

        var parameters = new List<string>
        {
            "pa=" + Uri.EscapeDataString(vpa),
            "pn=" + Uri.EscapeDataString(name),
        };

        if (request.Amount is { } amount)
        {
            if (!TryFormatAmount(amount, 0.01m, 9_999_999_999.99m, out var formatted))
                return PaymentQrResult.NotAvailable(rail, "This amount cannot be put into a UPI QR code.");

            parameters.Add("am=" + formatted);
        }

        parameters.Add("cu=INR");

        var note = Sanitise(request.Reference);
        if (note.Length > 0)
            parameters.Add("tn=" + Uri.EscapeDataString(Truncate(note, UpiMaxNoteLength)));

        return PaymentQrResult.Available(rail, "upi://pay?" + string.Join('&', parameters));
    }

    // ---------------------------------------------------------------------------------
    // EMVCo merchant-presented QR — shared by PIX and PromptPay
    // ---------------------------------------------------------------------------------

    private const string EmvPayloadFormatIndicator = "00";
    private const string EmvPointOfInitiation = "01";
    private const string EmvMerchantCategoryCode = "52";
    private const string EmvTransactionCurrency = "53";
    private const string EmvTransactionAmount = "54";
    private const string EmvCountryCode = "58";
    private const string EmvMerchantName = "59";
    private const string EmvMerchantCity = "60";
    private const string EmvAdditionalData = "62";
    private const string EmvCrc = "63";

    /// <summary>
    /// The four characters that introduce the checksum field: tag 63, length 04.
    ///
    /// <b>These are part of what is checksummed.</b> The CRC is computed over the entire
    /// payload up to and including this prefix, and only then are the four hex digits
    /// appended. Computing it over the payload without the prefix is the classic
    /// implementation bug, and it produces a code that no app can read.
    /// </summary>
    private const string EmvCrcPrefix = EmvCrc + "04";

    /// <summary>
    /// CRC-16/CCITT-FALSE over the UTF-8 bytes of <paramref name="payload"/>, as four
    /// uppercase hex digits. Polynomial 0x1021, initial value 0xFFFF, no input or output
    /// reflection, no final XOR.
    ///
    /// <para>
    /// Every one of those parameters is a trap. "CRC-16 with polynomial 0x1021" is shared by
    /// at least four incompatible variants — CCITT-FALSE, XMODEM (init 0x0000), KERMIT
    /// (reflected), X-25 (reflected with final XOR) — and three of them are what you get from
    /// a library whose name merely says "ccitt". They all produce plausible four-character
    /// checksums and every one of them makes the QR code unscannable, with no diagnostic
    /// beyond a banking app that shrugs.
    /// </para>
    ///
    /// <para>
    /// Public because it is the part of this file most worth testing directly, against the
    /// algorithm's published check value and against published scheme payloads.
    /// </para>
    /// </summary>
    public static string Crc16CcittFalse(string payload)
    {
        ushort crc = 0xFFFF;

        foreach (var b in Encoding.UTF8.GetBytes(payload))
        {
            crc ^= (ushort)(b << 8);

            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 0x8000) != 0
                    ? (ushort)((crc << 1) ^ 0x1021)
                    : (ushort)(crc << 1);
            }
        }

        return crc.ToString("X4", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// One EMVCo data object: two-digit ID, two-digit length, value.
    ///
    /// The length is the number of characters and it is always two digits, which is why
    /// everything written through here is truncated to its scheme limit first — a 100-character
    /// value cannot be expressed at all, and silently writing a three-digit length would
    /// corrupt every field that follows it.
    /// </summary>
    private static string Tlv(string id, string value) =>
        id + value.Length.ToString("D2", CultureInfo.InvariantCulture) + value;

    /// <summary>Appends tag 63 and the checksum that closes an EMVCo payload.</summary>
    private static string SealWithCrc(string body)
    {
        var withPrefix = body + EmvCrcPrefix;
        return withPrefix + Crc16CcittFalse(withPrefix);
    }

    // ---------------------------------------------------------------------------------
    // PIX — Brazil (BR Code)
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Globally Unique Identifier for the PIX arrangement inside the merchant account
    /// template. Lower case, matching the Banco Central's own published example; the scheme
    /// treats it case-insensitively.
    /// </summary>
    private const string PixGui = "br.gov.bcb.pix";

    private const string PixMerchantAccountTemplate = "26";
    private const string PixCurrency = "986";   // BRL, ISO 4217 numeric
    private const string PixCountry = "BR";

    /// <summary>
    /// "No category". The field is mandatory under EMVCo but PIX does not use it to route or
    /// price anything, and 0000 is the value the Banco Central's examples carry.
    /// </summary>
    private const string PixMerchantCategoryCode = "0000";

    /// <summary>
    /// The <c>txid</c> an app sends when the payee has not specified one. It is three
    /// asterisks, not an empty value: PIX defines <c>***</c> as "no transaction identifier",
    /// whereas an empty tag 62 is simply malformed.
    /// </summary>
    private const string PixNoTxid = "***";

    private const int PixMaxKeyLength = 77;
    private const int PixMaxTxidLength = 25;
    private const int EmvMaxMerchantNameLength = 25;
    private const int EmvMaxMerchantCityLength = 15;

    /// <summary>
    /// Builds a static PIX "BR Code" — the EMVCo merchant-presented payload Brazilian banking
    /// apps scan.
    ///
    /// <para>
    /// Tag 01, point-of-initiation, is deliberately omitted, which means the default:
    /// a reusable static code. The alternative, <c>12</c>, marks a code as single-use, and an
    /// app that honours it may refuse a second scan. On an invoice that is a real hazard —
    /// a client scans, gets interrupted, and comes back to a QR code their app now rejects —
    /// and the upside of the flag is nil, because nothing here can prevent a double payment
    /// anyway.
    /// </para>
    ///
    /// <para>
    /// The <c>txid</c> in tag 62 is reduced to letters and digits. The scheme restricts it to
    /// that set, so an invoice number like <c>INV-2026-0042</c> cannot go in as written; the
    /// hyphens are dropped rather than the reference, because a slightly altered reference
    /// still reconciles by eye and by prefix search, whereas no reference at all means a
    /// payment arrives with nothing to match it to.
    /// </para>
    /// </summary>
    public static PaymentQrResult Pix(PaymentQrRequest request)
    {
        const PaymentRail rail = PaymentRail.Pix;

        // Sanitised but not compacted, for the same reason as a UPI ID: no form of PIX key —
        // CPF, CNPJ, phone, email or random EVP key — contains a space, so a space means the
        // value is not a key, and silently closing the gap could produce somebody else's.
        var key = Sanitise(request.AccountIdentifier);
        if (key.Length == 0)
            return PaymentQrResult.NotAvailable(rail, "A PIX QR code needs a PIX key, and none is set.");

        if (key.Any(char.IsWhiteSpace) || key.Length > PixMaxKeyLength)
            return PaymentQrResult.NotAvailable(rail, "This does not look like a PIX key.");

        var name = ToEmvText(request.PayeeName);
        if (name.Length == 0)
            return PaymentQrResult.NotAvailable(rail, "Your business name is needed before a PIX QR code can be produced.");

        if (!IsCurrency(request.CurrencyCode, "BRL"))
            return PaymentQrResult.NotAvailable(rail, "PIX QR codes can only carry real amounts.");

        var amountField = string.Empty;
        if (request.Amount is { } amount)
        {
            if (!TryFormatAmount(amount, 0.01m, 9_999_999_999.99m, out var formatted))
                return PaymentQrResult.NotAvailable(rail, "This amount cannot be put into a PIX QR code.");

            amountField = formatted;
        }

        var city = ToEmvText(request.PayeeCity);
        if (city.Length == 0)
            city = "NA";

        var txid = new string(ToEmvText(request.Reference).Where(char.IsAsciiLetterOrDigit).ToArray());
        if (txid.Length == 0)
            txid = PixNoTxid;

        var merchantAccount = Tlv("00", PixGui) + Tlv("01", key);

        var body = new StringBuilder()
            .Append(Tlv(EmvPayloadFormatIndicator, "01"))
            .Append(Tlv(PixMerchantAccountTemplate, merchantAccount))
            .Append(Tlv(EmvMerchantCategoryCode, PixMerchantCategoryCode))
            .Append(Tlv(EmvTransactionCurrency, PixCurrency));

        if (amountField.Length > 0)
            body.Append(Tlv(EmvTransactionAmount, amountField));

        body.Append(Tlv(EmvCountryCode, PixCountry))
            .Append(Tlv(EmvMerchantName, Truncate(name, EmvMaxMerchantNameLength)))
            .Append(Tlv(EmvMerchantCity, Truncate(city, EmvMaxMerchantCityLength)))
            .Append(Tlv(EmvAdditionalData, Tlv("05", Truncate(txid, PixMaxTxidLength))));

        return PaymentQrResult.Available(rail, SealWithCrc(body.ToString()));
    }

    // ---------------------------------------------------------------------------------
    // PromptPay — Thailand
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// The PromptPay credit-transfer Application Identifier. A different AID,
    /// <c>A000000677010112</c>, addresses the bill-payment scheme, which is the one that can
    /// carry a biller reference — see the remark on references in <see cref="PromptPay"/>.
    /// </summary>
    private const string PromptPayAid = "A000000677010111";

    private const string PromptPayMerchantAccountTemplate = "29";
    private const string PromptPayCurrency = "764";  // THB, ISO 4217 numeric
    private const string PromptPayCountry = "TH";

    /// <summary>Mobile number, in the 13-digit <c>0066…</c> form.</summary>
    private const string PromptPayMobileTag = "01";

    /// <summary>National ID or 13-digit tax ID.</summary>
    private const string PromptPayNationalIdTag = "02";

    /// <summary>15-digit e-wallet identifier.</summary>
    private const string PromptPayEWalletTag = "03";

    /// <summary>
    /// Builds a PromptPay payload — EMVCo again, with Thailand's own merchant account
    /// template and the same CRC.
    ///
    /// <para>
    /// The identifier is normalised to the 13-digit <c>0066</c> form: <c>0066</c> followed by
    /// the subscriber number with its national trunk <c>0</c> removed. <c>0812345678</c>
    /// becomes <c>0066812345678</c>. The length is what makes this unambiguous to a reader,
    /// so <c>+66</c>, spaces, hyphens and the plain local form all have to converge on exactly
    /// those 13 digits — a 12- or 14-digit value in tag 01 is read as a different kind of
    /// identifier, or not read at all.
    /// </para>
    ///
    /// <para>
    /// Tag 01 at the top level does carry the point-of-initiation here, because Thai apps use
    /// it to decide whether to let the payer edit the amount: <c>12</c> when an amount is
    /// pre-filled, <c>11</c> when the code is reusable and the payer types the amount.
    /// </para>
    ///
    /// <para>
    /// <b>PromptPay carries no reference and no payee name.</b> The credit-transfer template
    /// has nowhere to put either, and the invoice number cannot be smuggled into one: that
    /// needs the bill-payment AID with a registered Biller ID, which a freelancer does not
    /// have. The payment therefore arrives identified only by the payer, and
    /// <see cref="PaymentQrRequest.Reference"/> and <see cref="PaymentQrRequest.PayeeName"/>
    /// are ignored rather than being wedged into a field Thai apps do not read.
    /// </para>
    /// </summary>
    public static PaymentQrResult PromptPay(PaymentQrRequest request)
    {
        const PaymentRail rail = PaymentRail.PromptPay;

        var raw = new string((request.AccountIdentifier ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        if (raw.Length == 0)
            return PaymentQrResult.NotAvailable(rail, "A PromptPay QR code needs a PromptPay ID, and none is set.");

        if (!TryNormalisePromptPayId(raw, out var idTag, out var idValue))
            return PaymentQrResult.NotAvailable(rail,
                "This does not look like a PromptPay ID. It should be a Thai mobile number, a 13-digit national or tax ID, or a 15-digit e-wallet ID.");

        if (!IsCurrency(request.CurrencyCode, "THB"))
            return PaymentQrResult.NotAvailable(rail, "PromptPay QR codes can only carry baht amounts.");

        var amountField = string.Empty;
        if (request.Amount is { } amount)
        {
            if (!TryFormatAmount(amount, 0.01m, 9_999_999_999.99m, out var formatted))
                return PaymentQrResult.NotAvailable(rail, "This amount cannot be put into a PromptPay QR code.");

            amountField = formatted;
        }

        var merchantAccount = Tlv("00", PromptPayAid) + Tlv(idTag, idValue);

        var body = new StringBuilder()
            .Append(Tlv(EmvPayloadFormatIndicator, "01"))
            .Append(Tlv(EmvPointOfInitiation, amountField.Length > 0 ? "12" : "11"))
            .Append(Tlv(PromptPayMerchantAccountTemplate, merchantAccount))
            .Append(Tlv(EmvCountryCode, PromptPayCountry))
            .Append(Tlv(EmvTransactionCurrency, PromptPayCurrency));

        if (amountField.Length > 0)
            body.Append(Tlv(EmvTransactionAmount, amountField));

        return PaymentQrResult.Available(rail, SealWithCrc(body.ToString()));
    }

    /// <summary>
    /// Works out which kind of PromptPay identifier a string of digits is, and puts a phone
    /// number into the 13-digit <c>0066</c> form.
    ///
    /// <para>
    /// A 13-digit value is ambiguous on its face — it is the length of both a normalised phone
    /// number and a national ID. The <c>0066</c> prefix resolves it, and safely: Thai national
    /// ID numbers begin with 1 to 8, so no real national ID can start with a zero, let alone
    /// with <c>0066</c>.
    /// </para>
    /// </summary>
    private static bool TryNormalisePromptPayId(string digits, out string tag, out string value)
    {
        tag = string.Empty;
        value = string.Empty;

        switch (digits)
        {
            // Already normalised: 0066 + nine subscriber digits.
            case { Length: 13 } when digits.StartsWith("0066", StringComparison.Ordinal):
                tag = PromptPayMobileTag;
                value = digits;
                return true;

            // International form without the 00 prefix: 66 + nine digits.
            case { Length: 11 } when digits.StartsWith("66", StringComparison.Ordinal):
                tag = PromptPayMobileTag;
                value = "00" + digits;
                return true;

            // Local form with the trunk zero: 0 + nine digits.
            case { Length: 10 } when digits[0] == '0':
                tag = PromptPayMobileTag;
                value = "0066" + digits[1..];
                return true;

            // Subscriber number alone.
            case { Length: 9 }:
                tag = PromptPayMobileTag;
                value = "0066" + digits;
                return true;

            // National ID or corporate tax ID. Both are 13 digits and both go in tag 02;
            // the scheme does not distinguish them.
            case { Length: 13 }:
                tag = PromptPayNationalIdTag;
                value = digits;
                return true;

            case { Length: 15 }:
                tag = PromptPayEWalletTag;
                value = digits;
                return true;

            default:
                return false;
        }
    }

    // ---------------------------------------------------------------------------------
    // Shared helpers
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Formats an amount as a decimal string with two places and a full stop, or refuses.
    ///
    /// <para>
    /// It refuses rather than rounds when the value has more than two decimal places. Rounding
    /// would be the friendly thing to do and it would be wrong: the QR code would then ask for
    /// a different amount than the invoice states, the payment would arrive short or over, and
    /// the invoice would never match. An amount that cannot be expressed exactly is a bug
    /// upstream, and the right response is no QR code.
    /// </para>
    ///
    /// <para>
    /// Two decimal places always, including on whole amounts, and
    /// <see cref="CultureInfo.InvariantCulture"/> always. A trailing zero may be dropped under
    /// both the EPC and EMVCo rules — the EPC spec's own example writes <c>EUR12.3</c> — but
    /// emitting the same shape every time is one fewer thing for a reader to disagree with us
    /// about, and it matches the amount printed on the invoice beside it.
    /// </para>
    /// </summary>
    private static bool TryFormatAmount(decimal amount, decimal min, decimal max, out string formatted)
    {
        formatted = string.Empty;

        if (amount < min || amount > max)
            return false;

        if (decimal.Round(amount, 2) != amount)
            return false;

        formatted = amount.ToString("0.00", CultureInfo.InvariantCulture);
        return true;
    }

    /// <summary>
    /// Strips control characters and collapses surrounding whitespace.
    ///
    /// The control characters are the point. A line feed inside a payee name would end the
    /// name element of an EPC payload early and shift every field after it, so that the
    /// remittance text becomes the IBAN; a user who pastes a name out of a spreadsheet can
    /// bring one along without ever seeing it. Removing them is not tidying, it is preventing
    /// a field-injection bug in a payment instruction.
    /// </summary>
    private static string Sanitise(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (char.IsControl(c))
            {
                // Keep words apart rather than running them together.
                if (builder.Length > 0 && builder[^1] != ' ') builder.Append(' ');
                continue;
            }

            builder.Append(c);
        }

        return builder.ToString().Trim();
    }

    /// <summary>Sanitises and removes every space — for identifiers that are written in groups.</summary>
    private static string Compact(string? value) =>
        new(Sanitise(value).Where(c => !char.IsWhiteSpace(c)).ToArray());

    /// <summary>
    /// Sanitises, then folds to plain ASCII, for the EMVCo text fields.
    ///
    /// EMVCo counts a field's length in characters while plenty of readers count the bytes
    /// they received, so a single accented character in a 25-character merchant name can put a
    /// reader's idea of where the next tag starts one byte out — and from there every
    /// remaining field, including the checksum, is misread. Folding <c>José</c> to <c>Jose</c>
    /// loses an accent; not folding it can lose the payment.
    /// </summary>
    private static string ToEmvText(string? value)
    {
        var sanitised = Sanitise(value);
        if (sanitised.Length == 0) return string.Empty;

        var decomposed = sanitised.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (!char.IsAscii(c)) continue;
            builder.Append(c);
        }

        return builder.ToString().Trim();
    }

    /// <summary>Cuts to a character limit. Trailing whitespace goes too, so a cut never ends mid-gap.</summary>
    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength].TrimEnd();

    private static bool IsCurrency(string? code, string expected) =>
        code is not null && string.Equals(code.Trim(), expected, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// ISO 13616 mod-97: move the first four characters to the end, map letters to two-digit
    /// numbers (A=10 … Z=35), and the result must be congruent to 1 modulo 97.
    ///
    /// Duplicated from the domain's payability check rather than shared, because
    /// <c>Akinai.Shared</c> does not reference <c>Akinai.Domain</c>. The algorithm is fixed by
    /// standard and cannot drift, but a reviewer should know the two copies exist.
    /// </summary>
    private static bool IsValidIban(string compacted)
    {
        var iban = compacted.ToUpperInvariant();

        // 15 (Norway) to 34 (the longest national formats) is the full legal range.
        if (iban.Length is < 15 or > 34) return false;
        if (!iban.All(char.IsAsciiLetterOrDigit)) return false;
        if (!char.IsAsciiLetter(iban[0]) || !char.IsAsciiLetter(iban[1])) return false;
        if (!char.IsAsciiDigit(iban[2]) || !char.IsAsciiDigit(iban[3])) return false;

        return Mod97(iban[4..] + iban[..4]) == 1;
    }

    /// <summary>ISO 9362: 4 letters bank, 2 letters country, 2 alphanumerics location, optional 3-character branch.</summary>
    private static bool IsValidBic(string compacted)
    {
        var bic = compacted.ToUpperInvariant();

        if (bic.Length is not (8 or 11)) return false;
        if (!bic.Take(4).All(char.IsAsciiLetter)) return false;
        if (!bic.Skip(4).Take(2).All(char.IsAsciiLetter)) return false;
        if (!bic.Skip(6).All(char.IsAsciiLetterOrDigit)) return false;

        return true;
    }

    /// <summary>
    /// ISO 11649 structured creditor reference: <c>RF</c>, two check digits, then up to 21
    /// alphanumerics, checked with the same mod-97 arithmetic as an IBAN.
    ///
    /// The checksum is verified rather than assumed, because the consequence of being wrong
    /// runs the other way from the usual. A reference that merely looks structured would be
    /// placed in the structured remittance element, where banks pass it through as machine
    /// data; if it is malformed the payer's bank may reject the whole instruction. A reference
    /// that fails this test is still perfectly good free text, so it falls back to the
    /// unstructured element instead of failing the code.
    /// </summary>
    private static bool IsValidCreditorReference(string compacted)
    {
        var reference = compacted.ToUpperInvariant();

        if (reference.Length is < 5 or > 25) return false;
        if (!reference.StartsWith("RF", StringComparison.Ordinal)) return false;
        if (!char.IsAsciiDigit(reference[2]) || !char.IsAsciiDigit(reference[3])) return false;
        if (!reference.All(char.IsAsciiLetterOrDigit)) return false;

        return Mod97(reference[4..] + reference[..4]) == 1;
    }

    /// <summary>
    /// The mod-97 used by both ISO 13616 and ISO 11649. Taken nine digits at a time so it
    /// never needs an arbitrary-precision integer type.
    /// </summary>
    private static int Mod97(string rearranged)
    {
        var remainder = 0;

        foreach (var c in rearranged)
        {
            if (char.IsAsciiDigit(c))
            {
                remainder = (remainder * 10 + (c - '0')) % 97;
            }
            else
            {
                var mapped = c - 'A' + 10;
                remainder = (remainder * 100 + mapped) % 97;
            }
        }

        return remainder;
    }

    /// <summary>
    /// Shape check for a UPI virtual payment address: <c>handle@psp</c>, one at-sign, and
    /// nothing in it that would have to be escaped to survive a query string.
    ///
    /// Deliberately a shape check and not a whitelist of PSP handles. There are hundreds of
    /// them and they change, and rejecting a real UPI ID because our list is a year old would
    /// be the more common failure by far.
    /// </summary>
    private static bool IsValidVpa(string compacted)
    {
        if (compacted.Length is < 3 or > 255) return false;

        var at = compacted.IndexOf('@');
        if (at <= 0 || at != compacted.LastIndexOf('@') || at == compacted.Length - 1) return false;

        var handle = compacted[(at + 1)..];
        if (!handle.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-')) return false;

        var local = compacted[..at];
        return local.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_');
    }
}
