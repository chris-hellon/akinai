using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Notifications;
using Akinai.Shared.Dtos.Notifications;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Notifications;

public class GetNotificationsHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService) : IHandle<GetNotifications, NotificationListDto>
{
    public async Task<HttpResult<NotificationListDto>> Handle(GetNotifications request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<NotificationListDto>.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var query = db.Notifications.AsNoTracking().Where(n => n.UserId == userId);

        var unreadCount = await query.CountAsync(n => !n.IsRead, cancellationToken);

        if (request.UnreadOnly)
            query = query.Where(n => !n.IsRead);

        var take = Math.Clamp(request.Take, 1, 100);

        var entities = await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        var items = entities.Select(n => new NotificationDto
        {
            Id = n.Id,
            Type = n.Type.ToString(),
            Title = n.Title,
            Message = n.Message,
            IsRead = n.IsRead,
            CreatedAt = n.CreatedAt,
            Link = n.Link
        }).ToList();

        return HttpResult<NotificationListDto>.Ok(new NotificationListDto
        {
            Items = items,
            UnreadCount = unreadCount
        });
    }
}
