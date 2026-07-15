using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Domain.Enums;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Shared.Api.Jobs;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Infrastructure.Handlers.Jobs;

public class MarkJobCompleteHandler(IDbContextFactory<ApplicationDbContext> dbFactory) : IHandle<MarkJobComplete>
{
    public async Task<HttpResult> Handle(MarkJobComplete request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var job = await db.Jobs.FirstOrDefaultAsync(j => j.Id == request.Id, cancellationToken);
        if (job is null) return HttpResult.NotFound();

        job.SetStatus(JobStatus.Completed);
        await db.SaveChangesAsync(cancellationToken);
        return HttpResult.NoContent();
    }
}
