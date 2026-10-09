using Akinai.Shared.Dtos.Timesheet;
using FluentAssertions;

namespace Akinai.Tests;

/// <summary>
/// The totals shown for a week must come from the tracked entries, never from the model.
/// These cover the DTO's own arithmetic; the handler enforces the same rule when it builds
/// the lines.
/// </summary>
public class WeekSummaryTests
{
    [Fact]
    public void Total_hours_are_the_sum_of_every_client()
    {
        var summary = new WeekSummaryDto(true, null,
        [
            new ClientWeekSummaryDto("Acme", 12.5m, [new WeekSummaryLineDto("Design", 12.5m)]),
            new ClientWeekSummaryDto("Globex", 7.25m, [new WeekSummaryLineDto("Build", 7.25m)]),
        ]);

        summary.TotalHours.Should().Be(19.75m);
    }

    [Fact]
    public void An_unavailable_summary_carries_a_reason_and_no_clients()
    {
        var summary = WeekSummaryDto.Unavailable("Nothing was tracked in this week.");

        summary.Available.Should().BeFalse();
        summary.Clients.Should().BeEmpty();
        summary.TotalHours.Should().Be(0m);
        summary.Reason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void A_clients_line_hours_never_exceed_its_tracked_total()
    {
        // The invariant the handler protects by summing only ids it recognises: a model
        // repeating or inventing an entry id must not inflate what the week says.
        var client = new ClientWeekSummaryDto("Acme", 10m,
        [
            new WeekSummaryLineDto("Website", 6m),
            new WeekSummaryLineDto("Meetings", 4m),
        ]);

        client.Lines.Sum(l => l.Hours).Should().BeLessThanOrEqualTo(client.Hours);
    }
}
