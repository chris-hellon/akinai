using Akinai.Shared.Payments;
using FluentAssertions;

namespace Akinai.Tests;

/// <summary>
/// VietQR replaced a third-party image URL that carried the account number and holder name
/// in a query string. Building the payload here removes that, but it also moves the
/// responsibility for routing the money into this codebase — so most of what follows is
/// about the cases where no code should be produced at all.
///
/// A missing QR is a visible inconvenience next to a printed set of bank details. A QR
/// carrying the wrong BIN is a payment arriving at the wrong institution, which nobody
/// notices until it has.
/// </summary>
public class VietQrPayloadTests
{
    private static PaymentQrResult Build(
        string? bankCode = "TIMO",
        string? account = "01819794",
        decimal? amount = 19_605_118m,
        string? currency = "VND",
        string? reference = "Invoice INV-00006")
        => PaymentQrPayload.VietQr(
            new PaymentQrRequest(
                PayeeName: "CGH Web",
                AccountIdentifier: account,
                Amount: amount,
                CurrencyCode: currency,
                Reference: reference),
            bankCode);

    /// <summary>Walks the EMVCo tag-length-value structure and returns the top-level tags.</summary>
    private static Dictionary<string, string> Tags(string payload)
    {
        var result = new Dictionary<string, string>();
        var i = 0;
        while (i + 4 <= payload.Length)
        {
            var id = payload.Substring(i, 2);
            var length = int.Parse(payload.Substring(i + 2, 2));
            result[id] = payload.Substring(i + 4, length);
            i += 4 + length;
        }
        return result;
    }

    // -- what must not happen ---------------------------------------------------------

    [Fact]
    public void An_unknown_bank_code_produces_no_code_rather_than_a_guessed_bin()
    {
        // The whole safety property. Every other failure here is an inconvenience; this one
        // is the difference between no QR and a QR pointing at the wrong bank.
        var result = Build(bankCode: "NOTAREALBANK");

        result.IsAvailable.Should().BeFalse();
        result.Payload.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_missing_bank_code_produces_no_code(string? bankCode)
    {
        Build(bankCode: bankCode).IsAvailable.Should().BeFalse();
    }

    [Fact]
    public void A_non_dong_amount_is_refused()
    {
        // VietQR is a domestic rail. A GBP figure on one would be read as dong.
        Build(currency: "GBP").IsAvailable.Should().BeFalse();
    }

    [Fact]
    public void A_fractional_amount_is_refused_rather_than_rounded()
    {
        // VND has no minor unit. Rounding here would alter the sum being demanded, which is
        // not a rounding decision anybody asked for.
        Build(amount: 100_000.50m).IsAvailable.Should().BeFalse();
    }

    [Fact]
    public void An_account_number_that_is_not_digits_is_refused()
    {
        Build(account: "GB83 REVO 0099").IsAvailable.Should().BeFalse();
    }

    [Fact]
    public void A_missing_account_number_produces_no_code()
    {
        Build(account: null).IsAvailable.Should().BeFalse();
    }

    // -- the payload itself -----------------------------------------------------------

    [Fact]
    public void The_payload_carries_the_napas_template_bin_and_account()
    {
        var payload = Build().Payload!;
        var tags = Tags(payload);

        // Tag 38 is NAPAS's merchant account template: GUID, then the beneficiary
        // (bank BIN + account), then the service code.
        var merchantAccount = tags["38"];
        var inner = Tags(merchantAccount);

        inner["00"].Should().Be("A000000727");
        inner["02"].Should().Be("QRIBFTTA", "an invoice is paid to an account, never to a card");

        var beneficiary = Tags(inner["01"]);
        beneficiary["00"].Should().Be("963388", "Timo's NAPAS BIN");
        beneficiary["01"].Should().Be("01819794");
    }

    [Fact]
    public void The_payload_declares_dong_and_vietnam()
    {
        var tags = Tags(Build().Payload!);

        tags["53"].Should().Be("704");   // VND, ISO 4217 numeric
        tags["58"].Should().Be("VN");
    }

    [Fact]
    public void An_amount_is_whole_dong_with_no_decimal_point()
    {
        var tags = Tags(Build(amount: 19_605_118m).Payload!);

        tags["54"].Should().Be("19605118");
    }

    [Fact]
    public void A_code_with_an_amount_is_marked_single_use_and_one_without_is_not()
    {
        // 12 tells the app the code is dynamic and meant to be scanned once; 11 is a
        // standing code the payer types an amount into.
        Tags(Build(amount: 500_000m).Payload!)["01"].Should().Be("12");
        Tags(Build(amount: null).Payload!)["01"].Should().Be("11");
    }

    [Fact]
    public void The_invoice_reference_travels_in_the_purpose_field()
    {
        var tags = Tags(Build(reference: "Invoice INV-00006").Payload!);

        Tags(tags["62"])["08"].Should().Contain("INV-00006");
    }

    [Fact]
    public void The_payee_name_is_not_in_the_payload()
    {
        // NAPAS resolves the real account holder from the BIN and account number and shows
        // that. Sending our own copy would put a second, unverified name on the payer's
        // screen — and the holder's name on the wire — for no benefit.
        Build().Payload!.Should().NotContain("CGH Web");
    }

    [Fact]
    public void The_payload_ends_with_a_valid_checksum()
    {
        var payload = Build().Payload!;

        payload.Should().MatchRegex("6304[0-9A-F]{4}$");

        var body = payload[..^4];
        PaymentQrPayload.Crc16CcittFalse(body).Should().Be(payload[^4..]);
    }

    [Fact]
    public void Every_declared_length_matches_its_value()
    {
        // A length that disagrees with its value makes the whole payload unparseable, and
        // the failure shows up in the payer's app rather than here.
        var payload = Build().Payload!;
        var i = 0;
        while (i + 4 <= payload.Length)
        {
            var length = int.Parse(payload.Substring(i + 2, 2));
            (i + 4 + length).Should().BeLessThanOrEqualTo(payload.Length);
            i += 4 + length;
        }
        i.Should().Be(payload.Length, "the tags should consume the payload exactly");
    }

    // -- the bank table ---------------------------------------------------------------

    [Theory]
    [InlineData("VCB", "970436")]
    [InlineData("ICB", "970415")]
    [InlineData("BIDV", "970418")]
    [InlineData("TCB", "970407")]
    [InlineData("MB", "970422")]
    [InlineData("TIMO", "963388")]
    public void Known_bank_codes_resolve_to_their_published_bins(string code, string bin)
    {
        VietQrBanks.BinFor(code).Should().Be(bin);
    }

    [Fact]
    public void Bank_codes_are_matched_regardless_of_case()
    {
        VietQrBanks.BinFor("timo").Should().Be(VietQrBanks.BinFor("TIMO"));
        VietQrBanks.BinFor(" TIMO ").Should().Be(VietQrBanks.BinFor("TIMO"));
    }

    [Fact]
    public void Every_bin_in_the_table_is_six_digits()
    {
        foreach (var code in VietQrBanks.KnownCodes)
        {
            VietQrBanks.BinFor(code).Should().MatchRegex("^[0-9]{6}$", $"{code} must be a NAPAS BIN");
        }
    }

    [Fact]
    public void No_two_banks_share_a_bin()
    {
        // A duplicate would mean one of the two is wrong, and payments for one bank would
        // be built with the other's identifier.
        var bins = VietQrBanks.KnownCodes.Select(VietQrBanks.BinFor).ToList();

        bins.Should().OnlyHaveUniqueItems();
    }
}
