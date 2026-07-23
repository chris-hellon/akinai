using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.Enums;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Jobs;
using Akinai.Shared.Dtos.Jobs;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Jobs;

public class GetTimesheetJobsHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService)
    : IHandle<GetTimesheetJobs, List<TimesheetJobDto>>
{
    public async Task<HttpResult<List<TimesheetJobDto>>> Handle(GetTimesheetJobs request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<List<TimesheetJobDto>>.Ok([]);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var weekEnd = request.WeekStart.AddDays(6);

        // Get all active jobs
        var activeJobs = await db.Jobs
            .Include(j => j.Project)
                .ThenInclude(p => p.Client)
            .Where(j => j.Status == JobStatus.Active)
            .ToListAsync(cancellationToken);

        // Get job IDs that have time entries this week (regardless of status)
        var jobIdsWithEntries = await db.TimeEntries
            .Where(e => e.UserId == userId && e.Date >= request.WeekStart && e.Date <= weekEnd)
            .Select(e => e.JobId)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Get inactive jobs that have time entries this week
        var activeJobIds = activeJobs.Select(j => j.Id).ToHashSet();
        var inactiveJobIdsWithEntries = jobIdsWithEntries.Where(id => !activeJobIds.Contains(id)).ToList();

        var inactiveJobsWithEntries = await db.Jobs
            .Include(j => j.Project)
                .ThenInclude(p => p.Client)
            .Where(j => inactiveJobIdsWithEntries.Contains(j.Id))
            .ToListAsync(cancellationToken);

        // Combine and create DTOs
        var result = new List<TimesheetJobDto>();

        // Add active jobs
        result.AddRange(activeJobs.Select(j => new TimesheetJobDto(
            j.Id,
            j.Name,
            j.Project.Name,
            j.Project.Client.Name,
            j.GetEffectiveHourlyRate(),
            IsActive: true)));

        // Add inactive jobs with time entries (they should still be shown but not editable for new entries)
        result.AddRange(inactiveJobsWithEntries.Select(j => new TimesheetJobDto(
            j.Id,
            j.Name,
            j.Project.Name,
            j.Project.Client.Name,
            j.GetEffectiveHourlyRate(),
            IsActive: false)));

        return HttpResult<List<TimesheetJobDto>>.Ok(result);
    }
}
