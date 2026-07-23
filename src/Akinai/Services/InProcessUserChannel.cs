using Akinai.Client.Services;
using Akinai.Infrastructure.Services;
using Akinai.Shared.Dtos.Notifications;
using Akinai.Shared.Services;

namespace Akinai.Services;

/// <summary>
/// Realtime for pages running on Blazor Server.
///
/// The circuit is already inside this process, so instead of connecting to the
/// SignalR hub it subscribes directly to the bus that feeds the hub. This class
/// lives in the host project because it is the only one that can see both the
/// client's abstraction and the infrastructure's bus.
///
/// Events are raised for every user, so each circuit filters to its own.
/// </summary>
public sealed class InProcessUserChannel(
    IUserEventBus bus,
    ICurrentUserService currentUser) : IRealtimeUserChannel
{
    private Action<NotificationDto>? _onNotification;
    private Action? _onEntitlementsChanged;
    private string? _userId;

    public bool IsConnected => _userId is not null;

    public Task ConnectAsync(
        Action<NotificationDto> onNotification,
        Action onEntitlementsChanged,
        CancellationToken cancellationToken = default)
    {
        // Captured once: HttpContext is not available later in the circuit's life.
        _userId = currentUser.UserId;
        if (_userId is null) return Task.CompletedTask;

        _onNotification = onNotification;
        _onEntitlementsChanged = onEntitlementsChanged;

        bus.NotificationPublished += OnNotificationPublished;
        bus.EntitlementsChanged += OnEntitlementsChanged;
        return Task.CompletedTask;
    }

    private void OnNotificationPublished(string userId, NotificationDto notification)
    {
        if (userId == _userId) _onNotification?.Invoke(notification);
    }

    private void OnEntitlementsChanged(string userId)
    {
        if (userId == _userId) _onEntitlementsChanged?.Invoke();
    }

    public ValueTask DisposeAsync()
    {
        // Must unsubscribe: the bus is a singleton and would otherwise hold
        // every circuit that ever connected alive for the life of the process.
        bus.NotificationPublished -= OnNotificationPublished;
        bus.EntitlementsChanged -= OnEntitlementsChanged;
        return ValueTask.CompletedTask;
    }
}
