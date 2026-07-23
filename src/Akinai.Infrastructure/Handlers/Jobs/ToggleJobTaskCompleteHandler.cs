using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Jobs;
using Akinai.Shared.Dtos.Jobs;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Jobs;

public class ToggleJobTaskCompleteHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService) : IHandle<ToggleJobTaskComplete, JobTaskDto>
{
    public async Task<HttpResult<JobTaskDto>> Handle(ToggleJobTaskComplete request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult<JobTaskDto>.Unauthorized();

        var task = await db.JobTasks
            .FirstOrDefaultAsync(t => t.Id == request.TaskId && t.JobId == request.JobId && t.Job.Project.Client.UserId == userId, cancellationToken);

        if (task is null)
            return HttpResult<JobTaskDto>.NotFound();

        task.ToggleComplete();
        await db.SaveChangesAsync(cancellationToken);

        var result = new JobTaskDto(
            task.Id,
            task.Title,
            task.Description,
            task.IsComplete,
            task.Order,
            task.CompletedAt);

        return HttpResult<JobTaskDto>.Ok(result);
    }
}
