using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Domain.Enums;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Shared.Api.Jobs;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Infrastructure.Handlers.Jobs;

public class ReopenJobHandler(IDbContextFactory<ApplicationDbContext> dbFactory) : IHandle<ReopenJob>
{
    public async Task<HttpResult> Handle(ReopenJob request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var job = await db.Jobs.FirstOrDefaultAsync(j => j.Id == request.Id, cancellationToken);
        if (job is null) return HttpResult.NotFound();

        job.SetStatus(JobStatus.Active);
        await db.SaveChangesAsync(cancellationToken);
        return HttpResult.NoContent();
    }
}
