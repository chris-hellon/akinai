using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.Entities;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.TimeEntries;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.TimeEntries;

public class LogTimeEntryHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService)
    : IHandle<LogTimeEntry, Guid>
{
    public async Task<HttpResult<Guid>> Handle(LogTimeEntry request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<Guid>.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.TimeEntries
            .FirstOrDefaultAsync(e => e.JobId == request.JobId && e.Date == request.Date && e.UserId == userId, cancellationToken);

        if (existing is not null)
        {
            if (request.Hours == 0)
            {
                db.TimeEntries.Remove(existing);
            }
            else
            {
                // Entity is already tracked from the query, so just update properties
                // EF Core change tracking will detect the changes automatically
                existing.Update(request.Hours, request.Description);
            }
            await db.SaveChangesAsync(cancellationToken);
            return HttpResult<Guid>.Ok(existing.Id);
        }

        if (request.Hours == 0) return HttpResult<Guid>.Ok(Guid.Empty);

        var entry = TimeEntry.Create(
            request.JobId,
            request.Date,
            request.Hours,
            request.Description,
            userId);

        db.TimeEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
        return HttpResult<Guid>.Created(entry.Id);
    }
}
