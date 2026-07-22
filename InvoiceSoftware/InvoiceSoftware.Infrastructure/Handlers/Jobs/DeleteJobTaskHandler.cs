using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Shared.Api.Jobs;
using InvoiceSoftware.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Infrastructure.Handlers.Jobs;

public class DeleteJobTaskHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService) : IHandle<DeleteJobTask>
{
    public async Task<HttpResult> Handle(DeleteJobTask request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult.Unauthorized();

        var task = await db.JobTasks
            .FirstOrDefaultAsync(t => t.Id == request.TaskId && t.JobId == request.JobId && t.Job.Project.Client.UserId == userId, cancellationToken);

        if (task is null)
            return HttpResult.NotFound();

        db.JobTasks.Remove(task);
        await db.SaveChangesAsync(cancellationToken);

        return HttpResult.NoContent();
    }
}
