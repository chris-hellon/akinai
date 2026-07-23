using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.Enums;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Jobs;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Jobs;

public class UpdateJobHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService) : IHandle<UpdateJob>
{
    public async Task<HttpResult> Handle(UpdateJob request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult.Unauthorized();

        var job = await db.Jobs.FirstOrDefaultAsync(j => j.Id == request.Id && j.Project.Client.UserId == userId, cancellationToken);
        if (job is null) return HttpResult.NotFound();

        var status = Enum.Parse<JobStatus>(request.Status);
        var priority = Enum.TryParse<JobPriority>(request.Priority, out var p) ? p : JobPriority.Medium;

        job.Update(
            request.Name,
            request.Description,
            request.Notes,
            status,
            request.EstimatedHours,
            request.HourlyRateOverride,
            priority,
            request.StartDate,
            request.DueDate,
            request.SectionId);

        await db.SaveChangesAsync(cancellationToken);
        return HttpResult.NoContent();
    }
}
