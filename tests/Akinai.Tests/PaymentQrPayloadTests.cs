using System.Text;
using Akinai.Shared.Payments;
using FluentAssertions;

namespace Akinai.Tests;

/// <summary>
/// The payloads these build are scanned once, by somebody who will not check what their
/// banking app pre-filled, so almost everything here is an exact-string assertion rather
/// than a property check. Where a published payload exists — the Banco Central's BR Code
/// sample, the PromptPay reference implementation's test vectors, the EPC's own worked
/// example — the test reproduces it character for character, because "looks about right" is
/// not a standard a payment instruction can be held to.
/// </summary>
public class PaymentQrPayloadTests
{
    private const string GermanIban = "DE71110220330123456789";
    private const string GermanBic = "BHBLDEHHXXX";

    private static PaymentQrRequest EuroRequest(
        string? name = "Acme Consulting",
        string? iban = GermanIban,
        decimal? amount = 1234.56m,
        string? reference = "Invoice INV-2026-0042",
        string? bic = null,
        string? purpose = null) =>
        new(PayeeName: name,
            AccountIdentifier: iban,
            Amount: amount,
            CurrencyCode: "EUR",
            Reference: reference,
            BeneficiaryBic: bic,
            PurposeCode: purpose);

    // =================================================================================
    // CRC-16/CCITT-FALSE — the one thing that makes an EMVCo code scan or not
    // =================================================================================

    /// <summary>
    /// An independent CRC-16/CCITT-FALSE, written table-driven rather than bit-by-bit so it
    /// shares no code path with the implementation under test. If both agree on the published
    /// vectors below, the agreement means something; if the test simply called the production
    /// function it would only be asserting that the function equals itself.
    /// </summary>
    private static string ReferenceCrc(string text)
    {
        var table = new ushort[256];
        for (var i = 0; i < 256; i++)
        {
            var value = (ushort)(i << 8);
            for (var bit = 0; bit < 8; bit++)
                value = (ushort)((value & 0x8000) != 0 ? (value << 1) ^ 0x1021 : value << 1);
            table[i] = value;
        }

        ushort crc = 0xFFFF;
        foreach (var b in Encoding.UTF8.GetBytes(text))
            crc = (ushort)((crc << 8) ^ table[(crc >> 8) ^ b]);

        return crc.ToString("X4");
    }

    [Fact]
    public void The_CRC_matches_the_algorithms_published_check_value()
    {
        // The CRC catalogue defines each variant by its output over the ASCII string
        // "123456789". For CRC-16/CCITT-FALSE (poly 0x1021, init 0xFFFF, no reflection, no
        // final XOR) that check value is 0x29B1. The near-identical variants give different
        // answers here — XMODEM 0x31C3, KERMIT 0x2189, X-25 0x906E — so this single assertion
        // pins down which of them we implemented, which is the mistake that makes every PIX
        // and PromptPay code in the app unscannable.
        PaymentQrPayload.Crc16CcittFalse("123456789").Should().Be("29B1");
        ReferenceCrc("123456789").Should().Be("29B1");
    }

    [Fact]
    public void The_CRC_matches_the_Banco_Central_BR_Code_sample()
    {
        // The static BR Code sample from the Banco Central do Brasil's "Manual de Padrões
        // para Iniciação do Pix" (random EVP key, no amount, txid "***"), widely reproduced
        // by PIX libraries. Its published checksum is 1D3D.
        //
        // Note where the checksummed text stops: it includes the "6304" that introduces the
        // CRC field. Computing the CRC over the payload without that prefix is the standard
        // way this gets implemented wrongly, and it yields a different value entirely.
        const string upToAndIncludingCrcTag =
            "00020126580014br.gov.bcb.pix0136123e4567-e12b-12d1-a456-426655440000"
            + "5204000053039865802BR5913Fulano de Tal6008BRASILIA62070503***6304";

        PaymentQrPayload.Crc16CcittFalse(upToAndIncludingCrcTag).Should().Be("1D3D");
        ReferenceCrc(upToAndIncludingCrcTag).Should().Be("1D3D");
    }

    [Theory]
    // Vectors from the test suite of the PromptPay reference implementation
    // (dtinth/promptpay-qr), which is what Thai banking apps are in practice tested against.
    // Each string is the full published payload; the CRC is its last four characters and is
    // computed over everything before them.
    [InlineData("00020101021129370016A000000677010111011300668012345675802TH530376463046197")]
    [InlineData("00020101021129370016A000000677010111021311111111111115802TH530376463047B5A")]
    [InlineData("00020101021129390016A00000067701011103150123456789012345802TH530376463049781")]
    [InlineData("00020101021229370016A000000677010111011300660000000005802TH530376454044.226304E469")]
    public void The_CRC_matches_the_published_PromptPay_vectors(string publishedPayload)
    {
        var body = publishedPayload[..^4];
        var publishedChecksum = publishedPayload[^4..];

        body.Should().EndWith("6304", "the checksum field's tag and length are themselves checksummed");

        PaymentQrPayload.Crc16CcittFalse(body).Should().Be(publishedChecksum);
        ReferenceCrc(body).Should().Be(publishedChecksum);
    }

    // =================================================================================
    // EPC — SEPA Credit Transfer
    // =================================================================================

    [Fact]
    public void An_EPC_payload_reproduces_the_EPC069_12_worked_example()
    {
        // EPC069-12 v3.0, section 2.3, example "V1". Two differences from the printed
        // example, both permitted by the spec and both deliberate:
        //
        //  * version 002 rather than 001, because 002 is current and makes the BIC optional;
        //  * EUR12.30 rather than EUR12.3, because we always write two decimal places.
        //
        // Everything else — element order, the empty trailing elements being absent entirely,
        // the structured reference in element 10 — is as printed.
        var result = PaymentQrPayload.Epc(new PaymentQrRequest(
            PayeeName: "Franz Mustermänn",
            AccountIdentifier: GermanIban,
            Amount: 12.30m,
            CurrencyCode: "EUR",
            Reference: "RF18539007547034",
            BeneficiaryBic: GermanBic,
            PurposeCode: "GDDS"));

        result.IsAvailable.Should().BeTrue();
        result.Payload.Should().Be(
            "BCD\n"
            + "002\n"
            + "1\n"
            + "SCT\n"
            + "BHBLDEHHXXX\n"
            + "Franz Mustermänn\n"
            + "DE71110220330123456789\n"
            + "EUR12.30\n"
            + "GDDS\n"
            + "RF18539007547034");

        // The spec states its own example is 95 characters and 96 UTF-8 bytes, with nine line
        // feeds. Ours is one character longer for the trailing zero on the amount, and the
        // byte count exceeds the character count by exactly one because of the "ä".
        result.Payload!.Split('\n').Should().HaveCount(10);
        result.Payload.Length.Should().Be(96);
        Encoding.UTF8.GetByteCount(result.Payload).Should().Be(97);
    }

    [Fact]
    public void An_EPC_payload_stops_at_the_last_populated_element()
    {
        // No amount, no reference, no BIC: the spec says the last populated element carries no
        // separator after it, so the payload must end at the IBAN with no trailing blank
        // lines. A reader that counts elements would otherwise see empty values in fields we
        // never meant to send.
        var result = PaymentQrPayload.Epc(EuroRequest(amount: null, reference: null));

        result.Payload.Should().Be(
            "BCD\n002\n1\nSCT\n\nAcme Consulting\n" + GermanIban);
        result.Payload!.Split('\n').Should().HaveCount(7);
        result.Payload.Should().NotEndWith("\n");
    }

    [Fact]
    public void An_omitted_BIC_still_occupies_its_line()
    {
        // The counterpart to the rule above: an empty element in the middle must remain as an
        // empty line. Drop it and the name slides into the BIC's position, the IBAN into the
        // name's, and the payer is shown a payment to "DE711102…" at a bank called
        // "Acme Consulting".
        var result = PaymentQrPayload.Epc(EuroRequest(bic: null));

        var lines = result.Payload!.Split('\n');
        lines[4].Should().BeEmpty();
        lines[5].Should().Be("Acme Consulting");
        lines[6].Should().Be(GermanIban);
    }

    [Fact]
    public void A_long_beneficiary_name_is_truncated_to_seventy_characters()
    {
        var longName = new string('A', 90);

        var result = PaymentQrPayload.Epc(EuroRequest(name: longName, reference: null));

        result.Payload!.Split('\n')[5].Should().Be(new string('A', 70));
    }

    [Fact]
    public void Long_remittance_text_is_truncated_to_a_hundred_and_forty_characters()
    {
        var longReference = new string('B', 200);

        var result = PaymentQrPayload.Epc(EuroRequest(reference: longReference));

        result.Payload!.Split('\n')[^1].Should().Be(new string('B', 140));
    }

    [Fact]
    public void An_EPC_payload_never_exceeds_the_three_hundred_and_thirty_one_byte_ceiling()
    {
        // 70 characters of name plus 140 of remittance plus the fixed elements overruns the
        // version-13 QR code the scheme mandates, so the free text has to give.
        var result = PaymentQrPayload.Epc(EuroRequest(
            name: new string('Ä', 70),
            reference: new string('Ö', 140),
            bic: GermanBic));

        result.IsAvailable.Should().BeTrue();
        Encoding.UTF8.GetByteCount(result.Payload!).Should().BeLessThanOrEqualTo(331);
        result.Payload!.Split('\n')[^1].Length.Should().BeLessThan(140, "the remittance text absorbed the overrun");
    }

    [Fact]
    public void A_valid_ISO_11649_reference_goes_in_the_structured_element()
    {
        var result = PaymentQrPayload.Epc(EuroRequest(reference: "RF18 5390 0754 7034"));

        var lines = result.Payload!.Split('\n');
        lines.Should().HaveCount(10);
        lines[9].Should().Be("RF18539007547034", "spaces are grouping, not part of the reference");
    }

    [Fact]
    public void A_reference_that_only_looks_structured_falls_back_to_free_text()
    {
        // RF48… has the right shape and the wrong check digits. Placed in the structured
        // element a bank may reject the instruction outright; as free text it is harmless and
        // still readable by the person reconciling the payment.
        var result = PaymentQrPayload.Epc(EuroRequest(reference: "RF48539007547034"));

        var lines = result.Payload!.Split('\n');
        lines.Should().HaveCount(11);
        lines[9].Should().BeEmpty("the structured element must stay empty — the two are mutually exclusive");
        lines[10].Should().Be("RF48539007547034");
    }

    [Fact]
    public void The_structured_and_unstructured_remittance_elements_are_never_both_populated()
    {
        foreach (var reference in new[] { "RF18539007547034", "Invoice 42", "RF48539007547034", null })
        {
            var lines = PaymentQrPayload.Epc(EuroRequest(reference: reference)).Payload!.Split('\n');

            var structured = lines.Length > 9 ? lines[9] : string.Empty;
            var unstructured = lines.Length > 10 ? lines[10] : string.Empty;

            (structured.Length > 0 && unstructured.Length > 0).Should().BeFalse();
        }
    }

    [Fact]
    public void A_purpose_code_that_is_not_four_alphanumerics_is_dropped()
    {
        // A bank screening a payment against a nonsense purpose code is a delay we caused.
        var result = PaymentQrPayload.Epc(EuroRequest(purpose: "not-a-code", reference: null));

        result.Payload!.Split('\n').Should().HaveCount(8, "the payload ends at the amount");
    }

    [Fact]
    public void A_newline_pasted_into_a_name_cannot_forge_an_element()
    {
        // The worst-case input for a line-delimited format. Were the line feeds kept, the
        // name element would end early and "EUR9999999.99" would be read as the IBAN.
        var result = PaymentQrPayload.Epc(EuroRequest(
            name: "Acme\nDE00000000000000000000\nEUR9999999.99",
            reference: null));

        result.Payload!.Split('\n').Should().HaveCount(8);
        result.Payload!.Split('\n')[5].Should().Be("Acme DE00000000000000000000 EUR9999999.99");
    }

    [Theory]
    [InlineData(100, "EUR100.00")]            // a whole number still gets two places
    [InlineData(1234.56, "EUR1234.56")]
    [InlineData(0.01, "EUR0.01")]             // the scheme floor
    [InlineData(999999999.99, "EUR999999999.99")]  // the scheme ceiling
    public void EPC_amounts_are_formatted_with_two_decimal_places_and_a_full_stop(decimal amount, string expected)
    {
        var result = PaymentQrPayload.Epc(EuroRequest(amount: amount, reference: null));

        result.Payload!.Split('\n')[7].Should().Be(expected);
    }

    [Fact]
    public void A_trailing_zero_beyond_two_places_is_not_treated_as_precision_loss()
    {
        // 12.340m carries a scale of three but the same value, so it is formattable.
        var result = PaymentQrPayload.Epc(EuroRequest(amount: 12.340m, reference: null));

        result.Payload!.Split('\n')[7].Should().Be("EUR12.34");
    }

    [Theory]
    [InlineData(1000000000.00)]  // above the scheme ceiling
    [InlineData(0)]              // below the floor: a QR code asking for nothing is a bug
    [InlineData(-5)]
    [InlineData(12.345)]         // rounding it would ask for a different amount than the invoice
    public void An_amount_that_cannot_be_expressed_exactly_yields_no_EPC_code(decimal amount)
    {
        var result = PaymentQrPayload.Epc(EuroRequest(amount: amount));

        result.IsAvailable.Should().BeFalse();
        result.Payload.Should().BeNull();
        result.UnavailableReason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void A_missing_business_name_yields_no_EPC_code()
    {
        var result = PaymentQrPayload.Epc(EuroRequest(name: "   "));

        result.IsAvailable.Should().BeFalse();
        result.UnavailableReason.Should().Contain("business name");
    }

    [Fact]
    public void A_missing_IBAN_yields_no_EPC_code()
    {
        var result = PaymentQrPayload.Epc(EuroRequest(iban: null));

        result.IsAvailable.Should().BeFalse();
        result.UnavailableReason.Should().Contain("IBAN");
    }

    [Fact]
    public void A_mistyped_IBAN_yields_no_EPC_code()
    {
        // One digit out. The mod-97 is the only thing standing between a typo and a QR code
        // that pays a stranger.
        var result = PaymentQrPayload.Epc(EuroRequest(iban: "DE71110220330123456788"));

        result.IsAvailable.Should().BeFalse();
        result.UnavailableReason.Should().Contain("checksum");
    }

    [Fact]
    public void An_IBAN_from_outside_SEPA_yields_no_EPC_code()
    {
        // A valid Turkish IBAN. The payload would be well-formed and the transfer impossible.
        var result = PaymentQrPayload.Epc(EuroRequest(iban: "TR330006100519786457841326"));

        result.IsAvailable.Should().BeFalse();
        result.UnavailableReason.Should().Contain("SEPA");
    }

    [Fact]
    public void A_non_EEA_IBAN_without_a_BIC_yields_no_EPC_code()
    {
        // The UK is a SEPA participant outside the EEA, where EPC069-12 keeps the BIC
        // mandatory, so IBAN-only addressing is not available.
        var withoutBic = PaymentQrPayload.Epc(EuroRequest(iban: "GB33BUKB20201555555555", bic: null));
        withoutBic.IsAvailable.Should().BeFalse();
        withoutBic.UnavailableReason.Should().Contain("SWIFT/BIC");

        var withBic = PaymentQrPayload.Epc(EuroRequest(iban: "GB33BUKB20201555555555", bic: "BUKBGB22"));
        withBic.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public void A_malformed_BIC_yields_no_EPC_code()
    {
        var result = PaymentQrPayload.Epc(EuroRequest(bic: "NOPE"));

        result.IsAvailable.Should().BeFalse();
        result.UnavailableReason.Should().Contain("SWIFT/BIC");
    }

    [Theory]
    [InlineData("USD")]
    [InlineData("GBP")]
    [InlineData(null)]
    public void A_non_euro_invoice_yields_no_EPC_code(string? currency)
    {
        var result = PaymentQrPayload.Epc(EuroRequest() with { CurrencyCode = currency });

        result.IsAvailable.Should().BeFalse();
        result.UnavailableReason.Should().Contain("euro");
    }

    // =================================================================================
    // UPI — India
    // =================================================================================

    [Fact]
    public void A_UPI_payload_is_a_pay_URI_with_the_expected_parameters()
    {
        var result = PaymentQrPayload.Upi(new PaymentQrRequest(
            PayeeName: "Acme Consulting",
            AccountIdentifier: "acme@okhdfcbank",
            Amount: 2500m,
            CurrencyCode: "INR",
            Reference: "INV-2026-0042"));

        result.Payload.Should().Be(
            "upi://pay?pa=acme%40okhdfcbank&pn=Acme%20Consulting&am=2500.00&cu=INR&tn=INV-2026-0042");
    }

    [Fact]
    public void UPI_values_are_percent_encoded_rather_than_form_encoded()
    {
        // The two characters that matter. An unescaped "&" would end the payee name and start
        // a parameter; a space encoded as "+" would be shown literally by any app that treats
        // the query string as a plain URI rather than as an HTML form submission.
        var result = PaymentQrPayload.Upi(new PaymentQrRequest(
            PayeeName: "Smith & Jones Ltd",
            AccountIdentifier: "smith.jones@ybl",
            Amount: null,
            CurrencyCode: "inr",
            Reference: "Invoice #42 / 50% deposit"));

        result.Payload.Should().Be(
            "upi://pay?pa=smith.jones%40ybl&pn=Smith%20%26%20Jones%20Ltd&cu=INR&tn=Invoice%20%2342%20%2F%2050%25%20deposit");
        result.Payload.Should().NotContain("+");
    }

    [Fact]
    public void A_long_UPI_note_is_truncated_to_fifty_characters()
    {
        var result = PaymentQrPayload.Upi(new PaymentQrRequest(
            PayeeName: "Acme",
            AccountIdentifier: "acme@ybl",
            Amount: null,
            CurrencyCode: "INR",
            Reference: new string('C', 80)));

        result.Payload.Should().EndWith("tn=" + new string('C', 50));
    }

    [Theory]
    [InlineData(null, "UPI ID")]
    [InlineData("", "UPI ID")]
    [InlineData("DE71110220330123456789", "name@bank")]  // an IBAN is not a VPA
    [InlineData("acme@", "name@bank")]
    [InlineData("@ybl", "name@bank")]
    [InlineData("acme@one@two", "name@bank")]
    [InlineData("acme name@ybl", "name@bank")]
    public void An_unusable_UPI_ID_yields_no_UPI_code(string? identifier, string expectedInReason)
    {
        var result = PaymentQrPayload.Upi(new PaymentQrRequest(
            PayeeName: "Acme", AccountIdentifier: identifier, CurrencyCode: "INR"));

        result.IsAvailable.Should().BeFalse();
        result.UnavailableReason.Should().Contain(expectedInReason);
    }

    [Fact]
    public void A_non_rupee_invoice_yields_no_UPI_code()
    {
        var result = PaymentQrPayload.Upi(new PaymentQrRequest(
            PayeeName: "Acme", AccountIdentifier: "acme@ybl", CurrencyCode: "EUR"));

        result.IsAvailable.Should().BeFalse();
        result.UnavailableReason.Should().Contain("rupee");
    }

    // =================================================================================
    // PIX — Brazil
    // =================================================================================

    [Fact]
    public void A_PIX_payload_reproduces_the_Banco_Central_BR_Code_sample()
    {
        // Same published sample as the CRC test above, this time built from its inputs: the
        // strongest statement available that the TLV assembly, the field order and the
        // checksum are all right at once.
        var result = PaymentQrPayload.Pix(new PaymentQrRequest(
            PayeeName: "Fulano de Tal",
            AccountIdentifier: "123e4567-e12b-12d1-a456-426655440000",
            Amount: null,
            CurrencyCode: "BRL",
            Reference: null,
            PayeeCity: "BRASILIA"));

        result.Payload.Should().Be(
            "00020126580014br.gov.bcb.pix0136123e4567-e12b-12d1-a456-426655440000"
            + "5204000053039865802BR5913Fulano de Tal6008BRASILIA62070503***63041D3D");
    }

    [Fact]
    public void A_PIX_payload_carries_the_amount_in_tag_54_and_the_reference_as_a_txid()
    {
        var result = PaymentQrPayload.Pix(new PaymentQrRequest(
            PayeeName: "Consultoria Açaí Ltda",
            AccountIdentifier: "acme@example.com",
            Amount: 1500m,
            CurrencyCode: "BRL",
            Reference: "INV-2026-0042",
            PayeeCity: "São Paulo"));

        result.IsAvailable.Should().BeTrue();

        // Amount as a plain decimal string in its own tag, two places, full stop.
        result.Payload.Should().Contain("54071500.00");

        // txid reduced to letters and digits, which is all the scheme permits there.
        result.Payload.Should().Contain("6215" + "0511INV20260042");

        // Accents folded out of the EMVCo text fields: readers that count bytes rather than
        // characters would otherwise lose their place and misread every later tag.
        result.Payload.Should().Contain("5921Consultoria Acai Ltda");
        result.Payload.Should().Contain("6009Sao Paulo");
    }

    [Fact]
    public void A_PIX_payloads_checksum_covers_the_whole_payload_including_the_CRC_tag()
    {
        var result = PaymentQrPayload.Pix(new PaymentQrRequest(
            PayeeName: "Acme", AccountIdentifier: "acme@example.com",
            Amount: 42.5m, CurrencyCode: "BRL", Reference: "42", PayeeCity: "RECIFE"));

        var payload = result.Payload!;
        ReferenceCrc(payload[..^4]).Should().Be(payload[^4..]);
        payload[^8..^4].Should().Be("6304");
    }

    [Fact]
    public void A_long_PIX_merchant_name_and_city_are_truncated_to_the_EMVCo_limits()
    {
        var result = PaymentQrPayload.Pix(new PaymentQrRequest(
            PayeeName: new string('A', 40),
            AccountIdentifier: "acme@example.com",
            Amount: null,
            CurrencyCode: "BRL",
            PayeeCity: new string('B', 30)));

        // Two-digit lengths are all EMVCo has, so these limits are structural, not cosmetic.
        result.Payload.Should().Contain("5925" + new string('A', 25));
        result.Payload.Should().Contain("6015" + new string('B', 15));
    }

    [Fact]
    public void A_PIX_payload_substitutes_a_placeholder_city_rather_than_failing()
    {
        // Tag 60 is mandatory and no app shows it. Losing the payment code over it would be
        // absurd.
        var result = PaymentQrPayload.Pix(new PaymentQrRequest(
            PayeeName: "Acme", AccountIdentifier: "acme@example.com",
            Amount: null, CurrencyCode: "BRL", PayeeCity: null));

        result.IsAvailable.Should().BeTrue();
        result.Payload.Should().Contain("6002NA");
    }

    [Fact]
    public void A_missing_PIX_key_yields_no_PIX_code()
    {
        var result = PaymentQrPayload.Pix(new PaymentQrRequest(
            PayeeName: "Acme", AccountIdentifier: null, CurrencyCode: "BRL"));

        result.IsAvailable.Should().BeFalse();
        result.UnavailableReason.Should().Contain("PIX key");
    }

    [Theory]
    [InlineData("acme name@example.com")]  // no form of PIX key contains a space
    [InlineData("1234 5678 901")]
    public void A_PIX_key_with_a_space_in_it_yields_no_PIX_code(string key)
    {
        // Closing the gap silently would produce a different, possibly real, key — and a PIX
        // payment to the wrong key is instant and irreversible.
        var result = PaymentQrPayload.Pix(new PaymentQrRequest(
            PayeeName: "Acme", AccountIdentifier: key, CurrencyCode: "BRL"));

        result.IsAvailable.Should().BeFalse();
        result.UnavailableReason.Should().Contain("PIX key");
    }

    [Fact]
    public void A_missing_business_name_yields_no_PIX_code()
    {
        var result = PaymentQrPayload.Pix(new PaymentQrRequest(
            PayeeName: null, AccountIdentifier: "acme@example.com", CurrencyCode: "BRL"));

        result.IsAvailable.Should().BeFalse();
        result.UnavailableReason.Should().Contain("business name");
    }

    [Fact]
    public void A_non_real_invoice_yields_no_PIX_code()
    {
        var result = PaymentQrPayload.Pix(new PaymentQrRequest(
            PayeeName: "Acme", AccountIdentifier: "acme@example.com", CurrencyCode: "EUR"));

        result.IsAvailable.Should().BeFalse();
        result.UnavailableReason.Should().Contain("real");
    }

    // =================================================================================
    // PromptPay — Thailand
    // =================================================================================

    [Fact]
    public void A_PromptPay_payload_reproduces_the_reference_implementations_vector()
    {
        // dtinth/promptpay-qr, "Amount setting" test case: id "000-000-0000", amount 4.22.
        var result = PaymentQrPayload.PromptPay(new PaymentQrRequest(
            PayeeName: "Acme",
            AccountIdentifier: "000-000-0000",
            Amount: 4.22m,
            CurrencyCode: "THB",
            Reference: "INV-2026-0042"));

        result.Payload.Should().Be(
            "00020101021229370016A000000677010111011300660000000005802TH530376454044.226304E469");
    }

    [Theory]
    // Every one of these is the same Thai mobile number, and all must converge on the same
    // thirteen digits. The length is what tells a reader this is a phone number rather than a
    // national ID, so a stray trunk zero or a missing "00" is not a formatting nicety.
    [InlineData("0801234567")]
    [InlineData("080-123-4567")]
    [InlineData("+66 80 123 4567")]
    [InlineData("0066801234567")]
    [InlineData("801234567")]
    public void A_Thai_mobile_number_is_normalised_to_the_thirteen_digit_form(string identifier)
    {
        var result = PaymentQrPayload.PromptPay(new PaymentQrRequest(
            AccountIdentifier: identifier, CurrencyCode: "THB"));

        // The no-amount vector published by the reference implementation for 0801234567.
        result.Payload.Should().Be(
            "00020101021129370016A000000677010111011300668012345675802TH530376463046197");
    }

    [Fact]
    public void A_national_or_tax_ID_uses_the_other_merchant_account_sub_tag()
    {
        var result = PaymentQrPayload.PromptPay(new PaymentQrRequest(
            AccountIdentifier: "1-1111-11111-11-1", CurrencyCode: "THB"));

        result.Payload.Should().Be(
            "00020101021129370016A000000677010111021311111111111115802TH530376463047B5A");
    }

    [Fact]
    public void An_e_wallet_ID_uses_its_own_sub_tag()
    {
        var result = PaymentQrPayload.PromptPay(new PaymentQrRequest(
            AccountIdentifier: "012345678901234", CurrencyCode: "THB"));

        result.Payload.Should().Be(
            "00020101021129390016A00000067701011103150123456789012345802TH530376463049781");
    }

    [Fact]
    public void The_point_of_initiation_reflects_whether_an_amount_is_pre_filled()
    {
        // Thai apps use this to decide whether the payer may edit the amount.
        PaymentQrPayload.PromptPay(new PaymentQrRequest(
                AccountIdentifier: "0801234567", CurrencyCode: "THB"))
            .Payload.Should().StartWith("000201010211");

        PaymentQrPayload.PromptPay(new PaymentQrRequest(
                AccountIdentifier: "0801234567", Amount: 500m, CurrencyCode: "THB"))
            .Payload.Should().StartWith("000201010212");
    }

    [Fact]
    public void A_PromptPay_amount_is_formatted_to_two_places()
    {
        var result = PaymentQrPayload.PromptPay(new PaymentQrRequest(
            AccountIdentifier: "0801234567", Amount: 12000m, CurrencyCode: "THB"));

        result.Payload.Should().Contain("540812000.00");
        ReferenceCrc(result.Payload![..^4]).Should().Be(result.Payload![^4..]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abcdefg")]        // no digits at all
    [InlineData("12345")]          // too short to be anything
    [InlineData("12345678901234")] // fourteen digits is not a PromptPay identifier
    public void An_unusable_PromptPay_ID_yields_no_PromptPay_code(string? identifier)
    {
        var result = PaymentQrPayload.PromptPay(new PaymentQrRequest(
            AccountIdentifier: identifier, CurrencyCode: "THB"));

        result.IsAvailable.Should().BeFalse();
        result.Payload.Should().BeNull();
        result.UnavailableReason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void A_non_baht_invoice_yields_no_PromptPay_code()
    {
        var result = PaymentQrPayload.PromptPay(new PaymentQrRequest(
            AccountIdentifier: "0801234567", CurrencyCode: "EUR"));

        result.IsAvailable.Should().BeFalse();
        result.UnavailableReason.Should().Contain("baht");
    }

    // =================================================================================
    // Dispatch
    // =================================================================================

    [Fact]
    public void Build_dispatches_to_the_rail_it_is_given()
    {
        var request = EuroRequest();

        PaymentQrPayload.Build(PaymentRail.Epc, request).Payload
            .Should().Be(PaymentQrPayload.Epc(request).Payload);

        // The same request through the wrong rail is unavailable rather than wrong: a euro
        // invoice simply has no PIX, UPI or PromptPay code.
        foreach (var rail in new[] { PaymentRail.Upi, PaymentRail.Pix, PaymentRail.PromptPay })
        {
            var result = PaymentQrPayload.Build(rail, request);
            result.Rail.Should().Be(rail);
            result.IsAvailable.Should().BeFalse();
            result.UnavailableReason.Should().NotBeNullOrWhiteSpace();
        }
    }
}
