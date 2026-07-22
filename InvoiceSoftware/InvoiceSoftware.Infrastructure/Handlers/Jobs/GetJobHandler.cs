using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Shared.Api.Jobs;
using InvoiceSoftware.Shared.Dtos.Jobs;
using InvoiceSoftware.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Infrastructure.Handlers.Jobs;

public class GetJobHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService) : IHandle<GetJob, JobDetailDto?>
{
    public async Task<HttpResult<JobDetailDto?>> Handle(GetJob request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult<JobDetailDto?>.Unauthorized();

        var job = await db.Jobs
            .Include(j => j.Project)
                .ThenInclude(p => p.Client)
            .Include(j => j.Section)
            .Include(j => j.TimeEntries)
            .Include(j => j.Tasks)
            .FirstOrDefaultAsync(j => j.Id == request.Id && j.Project.Client.UserId == userId, cancellationToken);

        if (job is null) return HttpResult<JobDetailDto?>.NotFound();

        var tasks = job.Tasks
            .OrderBy(t => t.Order)
            .Select(t => new JobTaskDto(
                t.Id,
                t.Title,
                t.Description,
                t.IsComplete,
                t.Order,
                t.CompletedAt))
            .ToList();

        var result = new JobDetailDto(
            job.Id,
            job.ProjectId,
            job.Project.Name,
            job.Project.ClientId,
            job.Project.Client.Name,
            job.SectionId,
            job.Section?.Name,
            job.Name,
            job.Description,
            job.Notes,
            job.Status.ToString(),
            job.Priority.ToString(),
            job.StartDate,
            job.DueDate,
            job.EstimatedHours?.Value,
            job.HourlyRateOverride?.Value,
            job.TimeEntries.Sum(t => t.Hours.Value),
            job.TimeEntries.Where(t => t.IsBilled).Sum(t => t.Hours.Value),
            tasks);

        return HttpResult<JobDetailDto?>.Ok(result);
    }
}
