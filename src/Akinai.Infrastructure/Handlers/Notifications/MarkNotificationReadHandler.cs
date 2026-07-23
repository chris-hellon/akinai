using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Notifications;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Notifications;

public class MarkNotificationReadHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService) : IHandle<MarkNotificationRead>
{
    public async Task<HttpResult> Handle(MarkNotificationRead request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var notification = await db.Notifications
            .FirstOrDefaultAsync(n => n.Id == request.Id && n.UserId == userId, cancellationToken);

        if (notification is null)
            return HttpResult.NotFound();

        notification.MarkAsRead();
        await db.SaveChangesAsync(cancellationToken);

        return HttpResult.NoContent();
    }
}
