using Akinai.Shared.Compliance;
using FluentAssertions;
using static Akinai.Shared.Compliance.TaxNumberFormat;

namespace Akinai.Tests;

/// <summary>
/// The published numbers used below are specimen or widely documented registrations, kept
/// deliberately in preference to numbers we made up: a validator tested only against its
/// own inventions proves nothing except that it is self-consistent.
/// </summary>
public class TaxNumberFormatTests
{
    // ------------------------------------------------------------------
    // Valid numbers, several countries.
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("GB980780684", "GB")]   // HMRC specimen; satisfies the original mod-97 series
    [InlineData("GB727255821", "GB")]
    [InlineData("DE136695976", "DE")]
    [InlineData("DE123456788", "DE")]
    [InlineData("51824753556", "AU")]   // the ATO's own ABN, from its published worked example
    [InlineData("53004085616", "AU")]
    [InlineData("NL004495445B01", "NL")]
    [InlineData("FR40303265045", "FR")]
    [InlineData("IE6388047V", "IE")]
    [InlineData("ESB12345678", "ES")]
    [InlineData("ATU12345675", "AT")]
    [InlineData("IT12345678903", "IT")]
    [InlineData("SE123456789701", "SE")]
    [InlineData("BE0123456749", "BE")]
    [InlineData("CY12345678L", "CY")]
    [InlineData("PL1234567883", "PL")]
    public void Published_numbers_are_accepted(string number, string country)
        => Validate(number, country).Outcome.Should().Be(Outcome.Valid);

    [Fact]
    public void A_valid_result_carries_the_number_back_in_canonical_prefixed_form()
    {
        // Worth storing in place of what the user typed, so the same registration entered
        // three different ways compares equal later.
        Validate("gb 980 780 684", "GB").Normalised.Should().Be("GB980780684");
        Validate("980780684", "GB").Normalised.Should().Be("GB980780684");
    }

    [Fact]
    public void All_three_countries_with_a_real_checksum_report_that_they_verified_one()
    {
        Validate("GB980780684", "GB").ChecksumVerified.Should().BeTrue();
        Validate("51824753556", "AU").ChecksumVerified.Should().BeTrue();
        Validate("DE136695976", "DE").ChecksumVerified.Should().BeTrue();
    }

    [Fact]
    public void A_structure_only_country_is_valid_but_does_not_claim_a_verified_checksum()
    {
        // The distinction matters: "well-formed" is a weaker statement than "the check
        // digits add up", and the UI should not be able to conflate them by accident.
        var result = Validate("IT12345678903", "IT");

        result.IsValid.Should().BeTrue();
        result.ChecksumVerified.Should().BeFalse();
    }

    // ------------------------------------------------------------------
    // Right number, wrong country.
    // ------------------------------------------------------------------

    [Fact]
    public void A_number_valid_for_one_country_is_rejected_for_another()
    {
        // The commonest real mistake is not a typo, it is the wrong client's number
        // pasted in, and that number is perfectly valid — just not here.
        var result = Validate("FR40303265045", "DE");

        result.Outcome.Should().Be(Outcome.WrongShape);
        result.Message.Should().Contain("France");
    }

    [Fact]
    public void A_German_number_offered_as_Dutch_is_rejected_on_length()
    {
        Validate("DE136695976", "NL").IsRejected.Should().BeTrue();
    }

    [Fact]
    public void A_rejection_says_what_the_right_shape_would_have_been()
    {
        var result = Validate("12345", "DE");

        result.IsRejected.Should().BeTrue();
        result.ExpectedShape.Should().Contain("9 digits");
        result.Message.Should().Contain("9 digits");
    }

    // ------------------------------------------------------------------
    // Prefix present or absent, and messy input.
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("DE136695976")]
    [InlineData("136695976")]
    [InlineData("de136695976")]
    [InlineData("  DE 136 695 976  ")]
    [InlineData("DE 136.695.976")]
    [InlineData("de-136-695-976")]
    [InlineData("DE/136/695/976")]
    [InlineData("\tde 136695976\n")]
    public void The_prefix_is_optional_and_punctuation_is_ignored(string typed)
        => Validate(typed, "DE").IsValid.Should().BeTrue();

    [Theory]
    [InlineData("51 824 753 556")]      // how an ABN is actually printed
    [InlineData("51824753556")]
    [InlineData("AU 51 824 753 556")]   // people add the country code anyway
    [InlineData("au51824753556")]
    public void An_ABN_is_accepted_however_it_is_grouped(string typed)
        => Validate(typed, "AU").IsValid.Should().BeTrue();

    [Theory]
    [InlineData("EL123456783")]         // Greece's VAT prefix
    [InlineData("GR123456783")]         // ...and the ISO code the user can see on screen
    [InlineData("123456783")]
    [InlineData("el 123 456 783")]
    public void Greece_is_accepted_under_both_its_codes(string typed)
        => Validate(typed, "GR").IsValid.Should().BeTrue();

    [Fact]
    public void Greece_normalises_to_its_VAT_prefix_not_its_ISO_code()
    {
        // EL is what belongs on the invoice; GR is what the country list calls it.
        Validate("GR123456783", "GR").Normalised.Should().Be("EL123456783");
        PrefixFor("GR").Should().Be("EL");
    }

    // ------------------------------------------------------------------
    // Checksums, including deliberate breakage.
    // ------------------------------------------------------------------

    [Fact]
    public void A_UK_number_with_broken_check_digits_is_rejected_not_merely_queried()
    {
        // Right length, right prefix, arithmetic nonsense — the signature of a transposed
        // digit, and invisible to a length check.
        var result = Validate("GB123456789", "GB");

        result.Outcome.Should().Be(Outcome.WrongShape);
        result.Message.Should().Contain("check digits");
        result.Normalised.Should().BeNull();
    }

    [Fact]
    public void Transposing_two_digits_of_a_good_UK_number_is_caught()
    {
        Validate("GB980780684", "GB").IsValid.Should().BeTrue();
        Validate("GB980708684", "GB").IsRejected.Should().BeTrue();
    }

    [Fact]
    public void The_later_UK_series_with_fifty_five_added_is_also_accepted()
    {
        // Constructed rather than published, because it has to exercise one branch
        // specifically: GB123456727 satisfies the mod-97 relation only once 55 is added,
        // and the test above proves the plain relation rejects that same stem. Dropping
        // the second generation of the rule would reject most numbers issued this century.
        Validate("GB123456727", "GB").IsValid.Should().BeTrue();
    }

    [Fact]
    public void A_twelve_digit_UK_branch_number_is_checked_on_its_first_nine()
    {
        Validate("GB980780684001", "GB").IsValid.Should().BeTrue();
        Validate("GB123456789001", "GB").IsRejected.Should().BeTrue();
    }

    [Theory]
    [InlineData("GBGD001")]   // government department
    [InlineData("GBGD499")]
    [InlineData("GBHA501")]   // health authority
    [InlineData("GBHA999")]
    [InlineData("gd 001")]
    public void Government_department_and_health_authority_numbers_are_accepted(string typed)
    {
        // These are real VAT numbers that appear on real invoices and have no check
        // digits. Rejecting them locks public-sector clients out of the app.
        Validate(typed, "GB").IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("GBGD501")]   // GD is 000-499, so this is out of range
    [InlineData("GBHA499")]   // HA is 500-999
    [InlineData("GBGD1")]
    public void The_GD_and_HA_ranges_are_not_interchangeable(string typed)
        => Validate(typed, "GB").IsRejected.Should().BeTrue();

    [Fact]
    public void A_broken_ABN_is_rejected()
    {
        // The ATO's own ABN with its last digit nudged by one.
        Validate("51824753556", "AU").IsValid.Should().BeTrue();
        Validate("51824753557", "AU").IsRejected.Should().BeTrue();
    }

    [Theory]
    [InlineData("12345678901")]
    [InlineData("00000000000")]
    [InlineData("99999999999")]
    public void Eleven_arbitrary_digits_are_not_an_ABN(string typed)
        => Validate(typed, "AU").IsRejected.Should().BeTrue();

    [Fact]
    public void A_broken_German_number_is_rejected()
    {
        Validate("DE136695976", "DE").IsValid.Should().BeTrue();
        Validate("DE136695975", "DE").IsRejected.Should().BeTrue();
    }

    // ------------------------------------------------------------------
    // Not checked, which is not the same as invalid.
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("US")]
    [InlineData("CA")]
    [InlineData("JP")]
    [InlineData("XX")]
    [InlineData(null)]
    [InlineData("")]
    public void A_country_with_no_rule_is_not_checked_rather_than_rejected(string? country)
    {
        // A US client's EIN in this field is not a mistake we are entitled to flag, and a
        // form that reddens it teaches the user to ignore every other warning we give.
        var result = Validate("12-3456789", country);

        result.Outcome.Should().Be(Outcome.NotChecked);
        result.IsValid.Should().BeFalse();
        result.IsRejected.Should().BeFalse();
    }

    [Fact]
    public void Not_checked_names_the_country_so_the_message_is_not_a_shrug()
        => Validate("12-3456789", "US").Message.Should().Contain("United States");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" - . ")]
    public void An_empty_number_is_not_checked_because_absence_is_a_corridor_question(string? number)
    {
        // CorridorRules already reports a missing VAT number where it matters. Two
        // components shouting about the same empty field is worse than one.
        var result = Validate(number, "DE");

        result.Outcome.Should().Be(Outcome.NotChecked);
        result.IsRejected.Should().BeFalse();
        result.ExpectedShape.Should().NotBeNull();   // still worth offering as a hint
    }

    // ------------------------------------------------------------------
    // Shape coverage across the table.
    // ------------------------------------------------------------------

    [Fact]
    public void Every_EU_member_plus_GB_and_AU_has_a_rule()
    {
        var euMembers = Akinai.Shared.Geography.Countries.AllCountries
            .Where(c => c.IsEuVatArea)
            .Select(c => c.Code)
            .ToList();

        euMembers.Should().HaveCount(27);
        euMembers.Should().OnlyContain(code => HasRuleFor(code));

        HasRuleFor("GB").Should().BeTrue();
        HasRuleFor("AU").Should().BeTrue();
        HasRuleFor("US").Should().BeFalse();
    }

    [Fact]
    public void Every_rule_states_a_shape_and_illustrates_it()
    {
        // The description is the only thing standing between a rejected number and a user
        // with no idea what to do next, so it may not be empty and may not be abstract.
        foreach (var code in Akinai.Shared.Geography.Countries.AllCountries
                     .Select(c => c.Code)
                     .Where(code => HasRuleFor(code)))
        {
            var shape = ExpectedShapeFor(code);
            shape.Should().NotBeNullOrWhiteSpace();
            shape.Should().Contain("for example", $"the shape for {code} should show one");
        }
    }

    [Theory]
    [InlineData("AT", "ATU12345675")]
    [InlineData("BE", "BE0123456749")]
    [InlineData("BG", "BG123456789")]
    [InlineData("CY", "CY12345678L")]
    [InlineData("CZ", "CZ12345679")]
    [InlineData("DK", "DK12345674")]
    [InlineData("EE", "EE123456780")]
    [InlineData("ES", "ESB12345678")]
    [InlineData("FI", "FI12345671")]
    [InlineData("FR", "FR40303265045")]
    [InlineData("GR", "EL123456783")]
    [InlineData("HR", "HR12345678903")]
    [InlineData("HU", "HU12345676")]
    [InlineData("IE", "IE6388047V")]
    [InlineData("IT", "IT12345678903")]
    [InlineData("LT", "LT123456715")]
    [InlineData("LU", "LU12345613")]
    [InlineData("LV", "LV12345678901")]
    [InlineData("MT", "MT12345634")]
    [InlineData("NL", "NL004495445B01")]
    [InlineData("PL", "PL1234567883")]
    [InlineData("PT", "PT123456789")]
    [InlineData("RO", "RO1234567897")]
    [InlineData("SE", "SE123456789701")]
    [InlineData("SI", "SI12345679")]
    [InlineData("SK", "SK1234567890")]
    [InlineData("GB", "GB980780684")]
    [InlineData("DE", "DE123456788")]
    public void The_example_in_every_shape_description_actually_validates(string code, string example)
    {
        // A hint that would itself be rejected is worse than no hint, and this is the kind
        // of thing that rots silently when a pattern is tightened.
        ExpectedShapeFor(code).Should().Contain(example);
        Validate(example, code).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("IE1234567A")]      // 7 digits, 1 letter
    [InlineData("IE1234567AB")]     // 7 digits, 2 letters, issued since 2013
    [InlineData("IE8Z49289F")]      // the old form, with a letter in second position
    [InlineData("IE8*49289F")]      // ...and the forms using * or +
    [InlineData("IE8+49289F")]
    public void All_three_Irish_layouts_are_accepted(string typed)
        => Validate(typed, "IE").IsValid.Should().BeTrue();

    [Theory]
    [InlineData("LT123456715")]     // 9 digits, legal person
    [InlineData("LT123456789012")]  // 12 digits, natural person
    public void Lithuania_issues_two_lengths(string typed)
        => Validate(typed, "LT").IsValid.Should().BeTrue();

    [Theory]
    [InlineData("RO12")]            // genuinely short numbers exist and are not truncations
    [InlineData("RO1234567897")]
    public void Romania_is_variable_length(string typed)
        => Validate(typed, "RO").IsValid.Should().BeTrue();

    [Fact]
    public void A_Dutch_number_must_carry_its_B_separator()
    {
        Validate("NL004495445B01", "NL").IsValid.Should().BeTrue();
        Validate("NL00449544501", "NL").IsRejected.Should().BeTrue();
        Validate("NL004495445C01", "NL").IsRejected.Should().BeTrue();
    }

    [Fact]
    public void A_Belgian_number_must_begin_zero_or_one()
    {
        Validate("BE0123456749", "BE").IsValid.Should().BeTrue();
        Validate("BE2123456749", "BE").IsRejected.Should().BeTrue();
    }

    [Fact]
    public void A_Spanish_number_needs_at_least_one_letter()
    {
        Validate("ESB12345678", "ES").IsValid.Should().BeTrue();
        Validate("ES12345678Z", "ES").IsValid.Should().BeTrue();
        Validate("ES123456789", "ES").IsRejected.Should().BeTrue();
    }

    // ------------------------------------------------------------------
    // Normalisation on its own.
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("  de 136.695-976 ", "DE136695976")]
    [InlineData("gb/980/780/684", "GB980780684")]
    [InlineData("51 824 753 556", "51824753556")]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    public void Normalise_keeps_only_the_characters_that_carry_meaning(string? input, string expected)
        => Normalise(input).Should().Be(expected);

    [Fact]
    public void Unicode_digit_lookalikes_are_dropped_rather_than_matched()
    {
        // Full-width digits would otherwise satisfy \d and let a visually plausible but
        // unusable number through.
        Normalise("DE１３６６９５９７６").Should().Be("DE");
        Validate("DE１３６６９５９７６", "DE").IsRejected.Should().BeTrue();
    }
}
