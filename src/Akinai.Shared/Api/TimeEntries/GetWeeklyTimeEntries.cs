using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.TimeEntries;

namespace Akinai.Shared.Api.TimeEntries;

[Route("api/timeentries/week/{WeekStart}")]
public class GetWeeklyTimeEntries : IGet<List<TimeEntryDto>>
{
    [RouteParam]
    public DateOnly WeekStart { get; init; }
}
