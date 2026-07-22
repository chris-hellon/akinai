using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Shared.Api.Jobs;
using InvoiceSoftware.Shared.Dtos.Jobs;
using InvoiceSoftware.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Infrastructure.Handlers.Jobs;

public class UpdateJobTaskHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService) : IHandle<UpdateJobTask, JobTaskDto>
{
    public async Task<HttpResult<JobTaskDto>> Handle(UpdateJobTask request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult<JobTaskDto>.Unauthorized();

        var task = await db.JobTasks
            .FirstOrDefaultAsync(t => t.Id == request.TaskId && t.JobId == request.JobId && t.Job.Project.Client.UserId == userId, cancellationToken);

        if (task is null)
            return HttpResult<JobTaskDto>.NotFound();

        task.Update(request.Title, request.Description);
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
