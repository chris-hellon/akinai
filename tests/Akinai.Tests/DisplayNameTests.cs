using Akinai.Shared.Compliance;
using Akinai.Shared.Dunning;
using FluentAssertions;

namespace Akinai.Tests;

/// <summary>
/// Both of these turn a stored or serialised enum name into something a person can read.
/// The cases worth pinning are the fallbacks: a value the table has not been taught must
/// come through as written rather than be flattened to "Unknown", because on a delivery
/// record an unrecognised stage is still evidence, and "Unknown" is itself a real tax
/// treatment that means something quite different.
/// </summary>
public class DisplayNameTests
{
    [Theory]
    [InlineData("AdvanceNotice", "Advance notice")]
    [InlineData("GentleReminder", "Gentle reminder")]
    [InlineData("FirmReminder", "Firm reminder")]
    [InlineData("FinalNotice", "Final notice")]
    public void Every_rung_of_the_ladder_has_a_plain_name(string stored, string expected)
    {
        DunningStageNames.Describe(stored).Should().Be(expected);
    }

    [Fact]
    public void Every_stage_the_enum_declares_is_covered()
    {
        // The guard against adding a rung and leaving the record showing its raw name.
        // Judged by the PascalCase name coming apart: a single-word member could
        // legitimately describe to itself, so only multi-word ones prove a mapping exists.
        foreach (var stage in Enum.GetValues<DunningStage>())
        {
            var raw = stage.ToString();
            DunningStageNames.Describe(raw).Should().NotBeNullOrWhiteSpace();
            if (IsMultiWord(raw))
                DunningStageNames.Describe(raw).Should().NotBe(raw, $"{stage} has no plain name");
        }
    }

    [Fact]
    public void An_unrecognised_stage_is_shown_as_written()
    {
        DunningStageNames.Describe("SomeFutureRung").Should().Be("SomeFutureRung");
    }

    [Fact]
    public void Only_the_advance_notice_predates_the_due_date()
    {
        DunningStageNames.IsBeforeDue("AdvanceNotice").Should().BeTrue();
        DunningStageNames.IsBeforeDue("GentleReminder").Should().BeFalse();
        DunningStageNames.IsBeforeDue("FinalNotice").Should().BeFalse();
    }

    [Theory]
    [InlineData("Domestic", "Domestic")]
    [InlineData("EuReverseCharge", "EU reverse charge")]
    [InlineData("ExportFromEu", "Export from the EU")]
    [InlineData("CrossBorderNonEu", "Cross-border, outside the EU")]
    public void Treatments_read_as_words(string stored, string expected)
    {
        TreatmentNames.Describe(stored).Should().Be(expected);
    }

    [Fact]
    public void Every_treatment_the_enum_declares_is_covered()
    {
        foreach (var treatment in Enum.GetValues<CorridorRules.Treatment>())
        {
            var raw = treatment.ToString();
            TreatmentNames.Describe(treatment).Should().NotBeNullOrWhiteSpace();
            // Domestic is one word and rightly describes to itself; EuReverseCharge must not.
            if (IsMultiWord(raw))
                TreatmentNames.Describe(treatment).Should().NotBe(raw, $"{treatment} has no plain name");
        }
    }

    /// <summary>A PascalCase name carrying more than one word, so it cannot be left as-is.</summary>
    private static bool IsMultiWord(string name)
        => name.Skip(1).Any(char.IsUpper);

    [Fact]
    public void An_undetermined_corridor_says_so_rather_than_saying_Unknown()
    {
        // "Unknown" on a document reads as a system failure. The honest statement is that
        // there was not enough information about the corridor to reach a treatment.
        TreatmentNames.Describe("Unknown").Should().Be("Could not be determined");
    }

    [Fact]
    public void A_missing_treatment_is_distinct_from_an_undetermined_one()
    {
        TreatmentNames.Describe(null).Should().Be("Not assessed");
        TreatmentNames.Describe("").Should().Be("Not assessed");
    }
}
