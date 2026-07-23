using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.Enums;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Jobs;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Jobs;

public class MarkJobCompleteHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService) : IHandle<MarkJobComplete>
{
    public async Task<HttpResult> Handle(MarkJobComplete request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult.Unauthorized();

        var job = await db.Jobs.FirstOrDefaultAsync(j => j.Id == request.Id && j.Project.Client.UserId == userId, cancellationToken);
        if (job is null) return HttpResult.NotFound();

        job.SetStatus(JobStatus.Completed);
        await db.SaveChangesAsync(cancellationToken);
        return HttpResult.NoContent();
    }
}
