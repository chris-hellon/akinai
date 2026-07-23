using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.TimeEntries;
using Akinai.Shared.Dtos.TimeEntries;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.TimeEntries;

public class GetUnbilledTimeEntriesHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService) : IHandle<GetUnbilledTimeEntries, List<UnbilledTimeEntryDto>>
{
    public async Task<HttpResult<List<UnbilledTimeEntryDto>>> Handle(GetUnbilledTimeEntries request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult<List<UnbilledTimeEntryDto>>.Unauthorized();

        var entries = await db.TimeEntries
            .Include(e => e.Job)
                .ThenInclude(j => j.Project)
                    .ThenInclude(p => p.Client)
            .Include(e => e.Job)
                .ThenInclude(j => j.Section)
            .Where(e => e.InvoiceId == null && e.Job.Project.ClientId == request.ClientId && e.UserId == userId)
            .ToListAsync(cancellationToken);

        var result = entries.Select(e => new UnbilledTimeEntryDto(
            e.Id,
            e.JobId,
            e.Job.Name,
            e.Job.Project.Name,
            e.Job.Section?.Name,
            e.Date,
            e.Hours.Value,
            e.Job.GetEffectiveHourlyRate(),
            e.Job.Project.Client.Currency)).ToList();

        return HttpResult<List<UnbilledTimeEntryDto>>.Ok(result);
    }
}
