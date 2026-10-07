using Akinai.Domain.Services;
using FluentAssertions;
using static Akinai.Domain.Services.PayabilityCheck;

namespace Akinai.Tests;

/// <summary>
/// The point of this check is that it catches a mistyped IBAN before a payment is
/// attempted, so the tests use real published specimen IBANs and real typos rather than
/// strings that merely look plausible.
/// </summary>
public class PayabilityCheckTests
{
    // Published ISO 13616 specimens.
    [Theory]
    [InlineData("GB82 WEST 1234 5698 7654 32")]
    [InlineData("DE89 3704 0044 0532 0130 00")]
    [InlineData("FR14 2004 1010 0505 0001 3M02 606")]
    [InlineData("NL91ABNA0417164300")]
    [InlineData("ES91 2100 0418 4502 0005 1332")]
    public void Real_IBANs_pass(string iban)
        => IsValidIban(iban).Should().BeTrue();

    [Fact]
    public void A_single_transposed_pair_is_caught()
    {
        // ...7654 32 -> ...7654 23. The kind of thing that happens copying by hand, and
        // exactly what a checksum exists to catch.
        IsValidIban("GB82 WEST 1234 5698 7654 32").Should().BeTrue();
        IsValidIban("GB82 WEST 1234 5698 7654 23").Should().BeFalse();
    }

    [Theory]
    [InlineData("GB82 WEST 1234 5698 7654 33")]  // one digit out
    [InlineData("GB00 WEST 1234 5698 7654 32")]  // wrong check digits
    [InlineData("GB82WEST123456987654")]         // too short
    [InlineData("1B82 WEST 1234 5698 7654 32")]  // country must be letters
    [InlineData("GBX2 WEST 1234 5698 7654 32")]  // check digits must be digits
    public void Broken_IBANs_fail(string iban)
        => IsValidIban(iban).Should().BeFalse();

    [Theory]
    [InlineData("DEUTDEFF")]       // 8, no branch
    [InlineData("DEUTDEFF500")]    // 11, with branch
    [InlineData("NEDSZAJJ")]
    public void Real_BICs_pass(string bic)
        => IsValidBic(bic).Should().BeTrue();

    [Theory]
    [InlineData("DEUTDEF")]        // 7 — wrong length
    [InlineData("DEUTDEFF5")]      // 9 — wrong length
    [InlineData("DEU1DEFF")]       // bank code must be letters
    [InlineData("DEUTD1FF")]       // country must be letters
    public void Broken_BICs_fail(string bic)
        => IsValidBic(bic).Should().BeFalse();

    [Fact]
    public void An_invoice_with_nothing_to_pay_into_is_blocked()
    {
        var findings = Run(new Details());

        findings.Should().ContainSingle()
            .Which.Severity.Should().Be(Severity.Blocking);
    }

    [Fact]
    public void A_mistyped_IBAN_blocks_sending()
    {
        var findings = Run(new Details(Iban: "GB82 WEST 1234 5698 7654 23", Swift: "DEUTDEFF"));

        findings.Should().Contain(f =>
            f.Severity == Severity.Blocking && f.Field == nameof(Details.Iban));
    }

    [Fact]
    public void A_valid_IBAN_with_a_BIC_raises_nothing()
    {
        var findings = Run(new Details(Iban: "GB82 WEST 1234 5698 7654 32", Swift: "DEUTDEFF"));

        findings.Should().BeEmpty();
    }

    [Fact]
    public void A_missing_BIC_is_a_warning_not_a_block()
    {
        // Payable in principle; most banks just will not route it.
        var findings = Run(new Details(Iban: "GB82 WEST 1234 5698 7654 32"));

        findings.Should().ContainSingle()
            .Which.Should().Match<Finding>(f =>
                f.Severity == Severity.Warning && f.Field == nameof(Details.Swift));
    }

    [Fact]
    public void Paying_into_Vietnam_without_a_purpose_code_is_flagged()
    {
        var findings = Run(
            new Details(Iban: "GB82 WEST 1234 5698 7654 32", Swift: "DEUTDEFF"),
            destinationCountryCode: "VN");

        findings.Should().ContainSingle()
            .Which.Field.Should().Be(nameof(Details.PurposeCode));
    }

    [Fact]
    public void Supplying_the_purpose_code_clears_it()
    {
        var findings = Run(
            new Details(Iban: "GB82 WEST 1234 5698 7654 32", Swift: "DEUTDEFF", PurposeCode: "GOODS"),
            destinationCountryCode: "VN");

        findings.Should().BeEmpty();
    }

    [Fact]
    public void A_country_with_no_purpose_code_rule_is_left_alone()
    {
        var findings = Run(
            new Details(Iban: "GB82 WEST 1234 5698 7654 32", Swift: "DEUTDEFF"),
            destinationCountryCode: "DE");

        findings.Should().BeEmpty();
    }

    [Fact]
    public void An_unknown_destination_skips_country_rules_rather_than_guessing()
    {
        var findings = Run(
            new Details(Iban: "GB82 WEST 1234 5698 7654 32", Swift: "DEUTDEFF"),
            destinationCountryCode: null);

        findings.Should().BeEmpty();
    }

    [Fact]
    public void A_local_account_with_no_IBAN_is_acceptable()
    {
        // Domestic rails (UK sort code, US routing) have no IBAN and must not be blocked.
        var findings = Run(new Details(AccountNumber: "12345678", SortCode: "20-00-00"));

        findings.Should().BeEmpty();
    }
}
