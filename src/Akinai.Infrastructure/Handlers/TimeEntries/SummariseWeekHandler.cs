using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Timesheet;
using Akinai.Shared.Dtos.Timesheet;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.TimeEntries;

/// <summary>
/// Turns a week of tracked time into something readable, one group per client.
///
/// The same rule as the invoice summariser, for the same reason: the model decides which
/// entries belong together and what to call the group, and nothing else. Hours are summed
/// here from the entries the model named, so a hallucinated figure cannot reach the user —
/// and an entry the model forgot cannot vanish from the totals either.
/// </summary>
public class SummariseWeekHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService,
    ITimesheetSummariser summariser) : IHandle<SummariseWeek, WeekSummaryDto>
{
    public async Task<HttpResult<WeekSummaryDto>> Handle(
        SummariseWeek request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult<WeekSummaryDto>.Unauthorized();

        if (!summariser.IsConfigured)
            return HttpResult<WeekSummaryDto>.Ok(
                WeekSummaryDto.Unavailable("Summarising is not configured on this account."));

        var weekEnd = request.WeekStart.AddDays(6);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var entries = await db.TimeEntries
            .Include(e => e.Job).ThenInclude(j => j.Project).ThenInclude(p => p.Client)
            .Where(e => e.UserId == userId
                        && e.Date >= request.WeekStart
                        && e.Date <= weekEnd)
            .OrderBy(e => e.Date)
            .ToListAsync(cancellationToken);

        if (entries.Count == 0)
            return HttpResult<WeekSummaryDto>.Ok(
                WeekSummaryDto.Unavailable("Nothing was tracked in this week."));

        var clients = new List<ClientWeekSummaryDto>();

        // One call per client: a group that mixed two clients' work would be unusable for
        // the thing this is for, which is telling one client what you did for them.
        foreach (var group in entries.GroupBy(e => e.Job.Project.Client.Name).OrderBy(g => g.Key))
        {
            var clientEntries = group.ToList();

            var summary = await summariser.SummariseAsync(
                userId,
                group.Key,
                clientEntries.Select(e => new TimesheetEntry(
                    e.Id, e.Job.Name, e.Job.Project.Name, e.Description, e.Date, e.BillableHours)).ToList(),
                request.Tone,
                cancellationToken);

            var hoursById = clientEntries.ToDictionary(e => e.Id, e => e.BillableHours);
            var totalHours = clientEntries.Sum(e => e.BillableHours);

            var lines = summary.HasGroups
                ? summary.Groups
                    .Select(g => new WeekSummaryLineDto(
                        g.Description,
                        // Only ids that are really in this week's entries count, so a
                        // model that invents or repeats one cannot inflate the hours.
                        g.TimeEntryIds.Distinct().Where(hoursById.ContainsKey).Sum(id => hoursById[id])))
                    .Where(l => l.Hours > 0)
                    .ToList()
                : [];

            // Falling back to the raw rows is better than showing a client nothing because
            // the model was unavailable or declined.
            if (lines.Count == 0)
            {
                lines = clientEntries
                    .GroupBy(e => e.Description ?? e.Job.Name)
                    .Select(g => new WeekSummaryLineDto(g.Key, g.Sum(e => e.BillableHours)))
                    .ToList();
            }

            clients.Add(new ClientWeekSummaryDto(group.Key, totalHours, lines));
        }

        return HttpResult<WeekSummaryDto>.Ok(new WeekSummaryDto(true, null, clients));
    }
}
