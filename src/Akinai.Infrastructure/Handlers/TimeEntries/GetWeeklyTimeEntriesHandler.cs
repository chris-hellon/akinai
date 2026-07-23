using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.TimeEntries;
using Akinai.Shared.Dtos.TimeEntries;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.TimeEntries;

public class GetWeeklyTimeEntriesHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService)
    : IHandle<GetWeeklyTimeEntries, List<TimeEntryDto>>
{
    public async Task<HttpResult<List<TimeEntryDto>>> Handle(GetWeeklyTimeEntries request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<List<TimeEntryDto>>.Ok([]);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var weekEnd = request.WeekStart.AddDays(6);
        var entries = await db.TimeEntries
            .Include(e => e.Job)
                .ThenInclude(j => j.Project)
            .Where(e => e.UserId == userId && e.Date >= request.WeekStart && e.Date <= weekEnd)
            .ToListAsync(cancellationToken);

        var result = entries.Select(e => new TimeEntryDto(
            e.Id,
            e.JobId,
            e.Job.Name,
            e.Job.Project.Name,
            e.Date,
            e.Hours.Value,
            e.Description,
            e.IsBilled)).ToList();

        return HttpResult<List<TimeEntryDto>>.Ok(result);
    }
}
