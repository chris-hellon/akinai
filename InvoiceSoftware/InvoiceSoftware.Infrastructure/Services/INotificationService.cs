using InvoiceSoftware.Domain.Enums;

namespace InvoiceSoftware.Infrastructure.Services;

/// <summary>
/// Raises an in-app notification for a user: persists it and pushes it live over SignalR.
/// Handlers call this as a side effect after their own work has been saved.
/// </summary>
public interface INotificationService
{
    Task CreateAsync(
        string? userId,
        NotificationType type,
        string title,
        string message,
        string? link = null,
        LinkedEntityType? relatedEntityType = null,
        Guid? relatedEntityId = null,
        CancellationToken cancellationToken = default);
}
