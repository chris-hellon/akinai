using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Jobs;
using Akinai.Shared.Dtos.Jobs;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Jobs;

public class CreateJobTaskHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService) : IHandle<CreateJobTask, JobTaskDto>
{
    public async Task<HttpResult<JobTaskDto>> Handle(CreateJobTask request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult<JobTaskDto>.Unauthorized();

        var job = await db.Jobs
            .Include(j => j.Tasks)
            .FirstOrDefaultAsync(j => j.Id == request.JobId && j.Project.Client.UserId == userId, cancellationToken);

        if (job is null)
            return HttpResult<JobTaskDto>.NotFound();

        var task = job.AddTask(request.Title, request.Description);

        db.JobTasks.Add(task);
        await db.SaveChangesAsync(cancellationToken);

        var result = new JobTaskDto(
            task.Id,
            task.Title,
            task.Description,
            task.IsComplete,
            task.Order,
            task.CompletedAt);

        return HttpResult<JobTaskDto>.Created(result);
    }
}
