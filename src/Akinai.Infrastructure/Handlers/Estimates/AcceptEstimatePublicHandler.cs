using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Estimates;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Estimates;

public class AcceptEstimatePublicHandler(IDbContextFactory<ApplicationDbContext> dbFactory)
    : IHandle<AcceptEstimatePublic>
{
    public async Task<HttpResult> Handle(AcceptEstimatePublic request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var estimate = await db.Estimates
            .Include(e => e.LineItems)
            .FirstOrDefaultAsync(e => e.PublicAccessToken == request.Token, cancellationToken);

        if (estimate == null)
            return HttpResult.NotFound();

        estimate.Accept();
        await db.SaveChangesAsync(cancellationToken);
        return HttpResult.NoContent();
    }
}
