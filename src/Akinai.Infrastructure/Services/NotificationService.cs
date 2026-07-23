using Akinai.Domain.Entities;
using Akinai.Domain.Enums;
using Akinai.Infrastructure.Data;
using Akinai.Infrastructure.Hubs;
using Akinai.Shared.Dtos.Notifications;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Akinai.Infrastructure.Services;

public class NotificationService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    IHubContext<NotificationHub> hub,
    ILogger<NotificationService> logger,
    IUserEventBus bus) : INotificationService
{
    public async Task CreateAsync(
        string? userId,
        NotificationType type,
        string title,
        string message,
        string? link = null,
        LinkedEntityType? relatedEntityType = null,
        Guid? relatedEntityId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
            return;

        Notification notification;
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            notification = Notification.Create(userId, type, title, message, link, relatedEntityType, relatedEntityId);
            db.Notifications.Add(notification);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // A notification is a side effect — never fail the originating operation because of it.
            logger.LogError(ex, "Failed to persist notification of type {Type} for user {UserId}", type, userId);
            return;
        }

        var dto = new NotificationDto
        {
            Id = notification.Id,
            Type = notification.Type.ToString(),
            Title = notification.Title,
            Message = notification.Message,
            IsRead = false,
            CreatedAt = notification.CreatedAt,
            Link = notification.Link
        };

        try
        {
            await hub.Clients.User(userId).SendAsync("notification", dto, cancellationToken);
            // Blazor Server circuits never connect to the hub, so they get it here.
            bus.PublishNotification(userId, dto);
        }
        catch (Exception ex)
        {
            // Live push is best-effort; the notification is already persisted and will
            // appear on the next load / poll.
            logger.LogWarning(ex, "Failed to push live notification to user {UserId}", userId);
        }
    }
}
