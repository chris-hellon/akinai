using Akinai.Shared.Compliance;
using Akinai.Shared.Geography;
using FluentAssertions;
using static Akinai.Shared.Compliance.CorridorRules;

namespace Akinai.Tests;

public class CorridorRulesTests
{
    [Fact]
    public void A_UK_seller_invoicing_a_German_business_gets_reverse_charge()
    {
        // The headline case: without the statement and both VAT numbers this invoice is
        // not valid and the client cannot reclaim.
        var result = Assess("DE", "FR", buyerIsBusiness: true,
            sellerTaxNumber: "DE123456789", buyerTaxNumber: "FR12345678901", taxRate: 0m);

        result.Treatment.Should().Be(Treatment.EuReverseCharge);
        result.RequiredStatements.Should().ContainSingle().Which.Should().Be(ReverseChargeStatement);
        result.Findings.Should().BeEmpty();
    }

    [Fact]
    public void Reverse_charge_without_the_clients_VAT_number_is_blocking()
    {
        var result = Assess("DE", "FR", buyerIsBusiness: true,
            sellerTaxNumber: "DE123456789", buyerTaxNumber: null, taxRate: 0m);

        result.HasBlockingFindings.Should().BeTrue();
        result.Findings.Should().Contain(f => f.Message.Contains("client's VAT number"));
    }

    [Fact]
    public void Reverse_charge_without_your_own_VAT_number_is_blocking()
    {
        var result = Assess("DE", "FR", buyerIsBusiness: true,
            sellerTaxNumber: null, buyerTaxNumber: "FR12345678901", taxRate: 0m);

        result.HasBlockingFindings.Should().BeTrue();
    }

    [Fact]
    public void Reverse_charge_carrying_a_tax_rate_is_queried_but_not_blocked()
    {
        // Possible — domestic reverse charge regimes exist — so warn rather than refuse.
        var result = Assess("DE", "FR", buyerIsBusiness: true,
            sellerTaxNumber: "DE1", buyerTaxNumber: "FR1", taxRate: 20m);

        result.HasBlockingFindings.Should().BeFalse();
        result.Findings.Should().Contain(f => f.Severity == Severity.Warning);
    }

    [Fact]
    public void An_EU_consumer_is_not_reverse_charge()
    {
        var result = Assess("DE", "FR", buyerIsBusiness: false,
            sellerTaxNumber: "DE1", buyerTaxNumber: null, taxRate: 19m);

        result.Treatment.Should().Be(Treatment.EuConsumer);
        result.RequiredStatements.Should().BeEmpty();
        result.HasBlockingFindings.Should().BeFalse();
    }

    [Fact]
    public void Same_country_is_domestic_and_says_nothing_special()
    {
        var result = Assess("GB", "GB", buyerIsBusiness: true, "GB1", "GB2", taxRate: 20m);

        result.Treatment.Should().Be(Treatment.Domestic);
        result.RequiredStatements.Should().BeEmpty();
        result.Findings.Should().BeEmpty();
    }

    [Fact]
    public void An_EU_seller_billing_outside_the_EU_is_an_export()
    {
        var result = Assess("DE", "US", buyerIsBusiness: true, "DE1", null, taxRate: 0m);

        result.Treatment.Should().Be(Treatment.ExportFromEu);
        result.HasBlockingFindings.Should().BeFalse();
    }

    [Fact]
    public void A_UK_seller_billing_the_EU_is_not_reverse_charge_post_Brexit()
    {
        // The UK left the EU VAT area, so this is an export under UK rules, not an
        // intra-EU reverse charge. Getting this backwards is the classic post-Brexit error.
        var result = Assess("GB", "DE", buyerIsBusiness: true, "GB1", "DE1", taxRate: 0m);

        result.Treatment.Should().Be(Treatment.CrossBorderNonEu);
        result.RequiredStatements.Should().BeEmpty();
    }

    [Fact]
    public void A_missing_country_is_reported_rather_than_assumed()
    {
        var result = Assess("GB", null, buyerIsBusiness: true, "GB1", null, taxRate: 20m);

        result.Treatment.Should().Be(Treatment.Unknown);
        result.Findings.Should().Contain(f => f.Message.Contains("no country set"));
    }

    [Fact]
    public void All_twenty_seven_EU_members_are_in_the_VAT_area()
    {
        // Reverse charge hinges entirely on this list, so assert the count rather than
        // trusting that nobody fat-fingered an entry.
        Countries.AllCountries.Count(c => c.IsEuVatArea).Should().Be(27);
    }

    [Theory]
    [InlineData("GB", false)]
    [InlineData("NO", false)]  // EEA but not EU VAT area
    [InlineData("CH", false)]
    [InlineData("IE", true)]
    [InlineData("HR", true)]   // joined the eurozone later; in the VAT area throughout
    public void EU_VAT_area_membership_is_correct_at_the_edges(string code, bool expected)
        => Countries.IsEuVatArea(code).Should().Be(expected);

    [Theory]
    [InlineData("United Kingdom", "GB")]
    [InlineData("UK", "GB")]
    [InlineData("England", "GB")]
    [InlineData("USA", "US")]
    [InlineData("united states of america", "US")]
    [InlineData("Holland", "NL")]
    [InlineData("Viet Nam", "VN")]
    [InlineData("DE", "DE")]
    [InlineData("de", "DE")]
    [InlineData("Germany", "DE")]
    [InlineData("  France  ", "FR")]
    public void Free_text_countries_parse_to_codes(string text, string expected)
        => Countries.TryParse(text).Should().Be(expected);

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Narnia")]
    [InlineData("XX")]
    [InlineData("Somewhere in Europe")]
    public void Anything_ambiguous_parses_to_null_rather_than_a_guess(string? text)
        => Countries.TryParse(text).Should().BeNull();
}
