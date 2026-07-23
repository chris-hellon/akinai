using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Clients;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Clients;

public class DeleteClientHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService) : IHandle<DeleteClient>
{
    public async Task<HttpResult> Handle(DeleteClient request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == request.Id && c.UserId == userId, cancellationToken);
        if (client is null) return HttpResult.NotFound();

        client.Deactivate();
        await db.SaveChangesAsync(cancellationToken);
        return HttpResult.NoContent();
    }
}
