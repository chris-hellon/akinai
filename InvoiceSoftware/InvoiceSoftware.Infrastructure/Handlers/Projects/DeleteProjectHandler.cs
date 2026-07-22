using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Shared.Api.Projects;
using InvoiceSoftware.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Infrastructure.Handlers.Projects;

public class DeleteProjectHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService) : IHandle<DeleteProject>
{
    public async Task<HttpResult> Handle(DeleteProject request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult.Unauthorized();

        var project = await db.Projects
            .Include(p => p.Jobs)
                .ThenInclude(j => j.Tasks)
            .FirstOrDefaultAsync(p => p.Id == request.Id && p.Client.UserId == userId, cancellationToken);

        if (project is null)
            return HttpResult.NotFound();

        // Remove time entries associated with project's jobs
        var jobIds = project.Jobs.Select(j => j.Id).ToList();
        var timeEntries = await db.TimeEntries
            .Where(e => jobIds.Contains(e.JobId))
            .ToListAsync(cancellationToken);

        db.TimeEntries.RemoveRange(timeEntries);

        // Remove job tasks
        foreach (var job in project.Jobs)
        {
            db.JobTasks.RemoveRange(job.Tasks);
        }

        // Remove jobs
        db.Jobs.RemoveRange(project.Jobs);

        // Remove the project
        db.Projects.Remove(project);
        await db.SaveChangesAsync(cancellationToken);

        return HttpResult.NoContent();
    }
}
