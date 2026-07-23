using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.Jobs;

namespace Akinai.Shared.Api.Jobs;

[Route("api/jobs/timesheet/{WeekStart}")]
public class GetTimesheetJobs : IGet<List<TimesheetJobDto>>
{
    [RouteParam] public DateOnly WeekStart { get; set; }
}
