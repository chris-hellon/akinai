using FluentAssertions;
using Akinai.Domain.Services;

namespace Akinai.Tests;

/// <summary>
/// The rule that makes AI summarisation safe to put on an invoice.
///
/// A language model proposes how time entries are grouped and what to call each
/// group. It never proposes a number. These tests pin both halves of that: the
/// arithmetic is derived from the entries, and any grouping that does not
/// account for every entry exactly once is rejected outright rather than
/// repaired — because a repaired grouping silently drops or double-bills work.
/// </summary>
public class TimesheetGroupingTests
{
    private static readonly Guid Invoice = Guid.NewGuid();

    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();
    private static readonly Guid C = Guid.NewGuid();

    /// <summary>Three entries: two at £20/h, one at £50/h.</summary>
    private static readonly TimesheetGrouping.BillableEntry[] Entries =
    [
        new(A, BillableHours: 6.25m, HourlyRate: 20m),   // 125.00
        new(B, BillableHours: 3.75m, HourlyRate: 20m),   //  75.00
        new(C, BillableHours: 2.00m, HourlyRate: 50m),   // 100.00
    ];

    private static TimesheetGrouping.Result Build(params TimesheetGrouping.ProposedGroup[] groups)
        => TimesheetGrouping.Build(Invoice, Entries, groups);

    /// <summary>Names the type once — target-typed new() cannot infer in a params array.</summary>
    private static TimesheetGrouping.ProposedGroup G(string description, params Guid[] ids)
        => new(description, ids);

    [Fact]
    public void Sums_hours_and_amount_from_the_entries()
    {
        var result = Build(
            G("Front-end build", A, B),
            G("Architecture review", C));

        result.Accepted.Should().BeTrue();
        result.Lines.Should().HaveCount(2);

        result.Lines[0].Hours.Should().Be(10.00m);
        result.Lines[0].Amount.Should().Be(200.00m);
        result.Lines[1].Hours.Should().Be(2.00m);
        result.Lines[1].Amount.Should().Be(100.00m);
    }

    [Fact]
    public void Total_is_unchanged_by_how_the_work_is_grouped()
    {
        // The point of the feature is presentation. Whatever the model decides,
        // the client must owe exactly the same amount.
        var expected = Entries.Sum(e => e.BillableHours * e.HourlyRate);

        var oneLine = Build(G("Everything", A, B, C));
        var threeLines = Build(G("One", A), G("Two", B), G("Three", C));

        oneLine.Lines.Sum(l => l.Amount).Should().Be(expected);
        threeLines.Lines.Sum(l => l.Amount).Should().Be(expected);
    }

    [Fact]
    public void Blends_the_rate_when_a_group_spans_different_rates()
    {
        // 6.25h@20 + 2h@50 = 225.00 over 8.25h. Reporting either 20 or 50 would
        // misstate the work; the blend is the only honest single number.
        var result = Build(G("Mixed", A, C), G("Rest", B));

        result.Lines[0].Amount.Should().Be(225.00m);
        result.Lines[0].Hours.Should().Be(8.25m);
        result.Lines[0].EffectiveRate.Should().BeApproximately(27.27m, 0.01m);
    }

    [Fact]
    public void Rejects_a_grouping_that_leaves_an_entry_out()
    {
        // Unbilled work. Worse than an ugly invoice.
        var result = Build(G("Partial", A, B));

        result.Accepted.Should().BeFalse();
        result.Lines.Should().BeEmpty();
        result.Rejection.Should().Contain("2 of 3");
    }

    [Fact]
    public void Rejects_a_grouping_that_uses_an_entry_twice()
    {
        // Double-billing. The failure mode that must never reach a client.
        var result = Build(G("First", A, B), G("Second", B, C));

        result.Accepted.Should().BeFalse();
        result.Rejection.Should().Contain("more than one group");
    }

    [Fact]
    public void Rejects_an_entry_that_is_not_on_this_invoice()
    {
        var result = Build(G("Wrong", A, B, C, Guid.NewGuid()));

        result.Accepted.Should().BeFalse();
        result.Rejection.Should().Contain("not on this invoice");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_a_group_with_no_description(string description)
    {
        var result = Build(G(description, A, B, C));

        result.Accepted.Should().BeFalse();
    }

    [Fact]
    public void Rejects_an_empty_grouping()
    {
        TimesheetGrouping.Build(Invoice, Entries, []).Accepted.Should().BeFalse();
    }

    [Fact]
    public void Rejects_a_group_containing_no_entries()
    {
        var result = Build(G("Empty"), G("Rest", A, B, C));

        result.Accepted.Should().BeFalse();
        result.Rejection.Should().Contain("no entries");
    }

    [Fact]
    public void Orders_lines_as_proposed()
    {
        var result = Build(
            G("Second on the invoice", C),
            G("First on the invoice", A, B));

        result.Lines[0].Order.Should().Be(0);
        result.Lines[0].Description.Should().Be("Second on the invoice");
        result.Lines[1].Order.Should().Be(1);
    }
}
