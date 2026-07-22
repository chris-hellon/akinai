using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Domain.Enums;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Shared.Api.Jobs;
using InvoiceSoftware.Shared.Dtos.Jobs;
using InvoiceSoftware.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Infrastructure.Handlers.Jobs;

public class GetActiveJobsHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService) : IHandle<GetActiveJobs, List<ActiveJobDto>>
{
    public async Task<HttpResult<List<ActiveJobDto>>> Handle(GetActiveJobs request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult<List<ActiveJobDto>>.Unauthorized();

        var jobs = await db.Jobs
            .Include(j => j.Project)
                .ThenInclude(p => p.Client)
            .Where(j => j.Project.Client.UserId == userId)
            .Where(j => j.Status == JobStatus.Active)
            .ToListAsync(cancellationToken);

        var result = jobs.Select(j => new ActiveJobDto(
            j.Id,
            j.Name,
            j.Project.Name,
            j.Project.Client.Name,
            j.GetEffectiveHourlyRate())).ToList();

        return HttpResult<List<ActiveJobDto>>.Ok(result);
    }
}
