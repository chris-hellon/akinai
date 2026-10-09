using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.Timesheet;

namespace Akinai.Shared.Api.Timesheet;

/// <summary>
/// Rewrites a week of tracked time into something readable, grouped by client.
/// A preview only — nothing is written, and no invoice is involved.
/// </summary>
[Route("api/timesheet/summarise-week")]
public class SummariseWeek : IPost<WeekSummaryDto>
{
    /// <summary>Monday of the week being summarised.</summary>
    [BodyParam] public DateOnly WeekStart { get; set; }

    [BodyParam] public string Tone { get; set; } = Akinai.Shared.Ai.SummaryTone.Professional;
}
