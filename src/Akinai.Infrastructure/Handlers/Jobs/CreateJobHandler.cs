using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.Entities;
using Akinai.Domain.Enums;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Jobs;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Jobs;

public class CreateJobHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService) : IHandle<CreateJob, Guid>
{
    public async Task<HttpResult<Guid>> Handle(CreateJob request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var priority = Enum.TryParse<JobPriority>(request.Priority, out var p) ? p : JobPriority.Medium;

        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult<Guid>.Unauthorized();

        // The parent project must be the caller's.
        var ownsProject = await db.Projects.AnyAsync(p => p.Id == request.ProjectId && p.Client.UserId == userId, cancellationToken);
        if (!ownsProject) return HttpResult<Guid>.NotFound();

        var job = Job.Create(
            request.ProjectId,
            request.Name,
            request.Description,
            request.Notes,
            request.EstimatedHours,
            request.HourlyRateOverride,
            priority,
            request.StartDate,
            request.DueDate,
            request.SectionId);

        db.Jobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);
        return HttpResult<Guid>.Created(job.Id);
    }
}
