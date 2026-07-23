using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Jobs;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Jobs;

public class DeleteJobHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService) : IHandle<DeleteJob>
{
    public async Task<HttpResult> Handle(DeleteJob request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult.Unauthorized();

        var job = await db.Jobs
            .Include(j => j.Tasks)
            .FirstOrDefaultAsync(j => j.Id == request.Id && j.Project.Client.UserId == userId, cancellationToken);

        if (job is null)
            return HttpResult.NotFound();

        // Unlink time entries from the job
        var timeEntries = await db.TimeEntries
            .Where(e => e.JobId == request.Id)
            .ToListAsync(cancellationToken);

        foreach (var entry in timeEntries)
        {
            db.TimeEntries.Remove(entry);
        }

        // Remove job tasks
        db.JobTasks.RemoveRange(job.Tasks);

        // Remove the job
        db.Jobs.Remove(job);
        await db.SaveChangesAsync(cancellationToken);

        return HttpResult.NoContent();
    }
}
