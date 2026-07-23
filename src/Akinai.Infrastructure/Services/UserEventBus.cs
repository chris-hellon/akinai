using Akinai.Shared.Dtos.Notifications;

namespace Akinai.Infrastructure.Services;

/// <summary>
/// In-process fan-out of per-user live events.
///
/// The SignalR hub reaches browsers running WebAssembly. This reaches Blazor
/// Server circuits, which live in this same process and therefore never
/// connect to the hub at all. Everything that pushes to the hub publishes here
/// too, so a user gets the same events whichever runtime their page is on.
///
/// Singleton, and deliberately trivial: no queueing, no delivery guarantees.
/// A missed live event is not a lost event — every one of these is already
/// persisted, and the client re-reads on the next load.
/// </summary>
public interface IUserEventBus
{
    void PublishNotification(string userId, NotificationDto notification);

    void PublishEntitlementsChanged(string userId);

    /// <summary>Raised for every user; subscribers filter by the id themselves.</summary>
    event Action<string, NotificationDto>? NotificationPublished;

    event Action<string>? EntitlementsChanged;
}

/// <inheritdoc />
public sealed class UserEventBus : IUserEventBus
{
    public event Action<string, NotificationDto>? NotificationPublished;
    public event Action<string>? EntitlementsChanged;

    public void PublishNotification(string userId, NotificationDto notification)
    {
        // A subscriber that throws must not take down the publisher — this runs
        // as a side effect of saving real work.
        try { NotificationPublished?.Invoke(userId, notification); } catch { }
    }

    public void PublishEntitlementsChanged(string userId)
    {
        try { EntitlementsChanged?.Invoke(userId); } catch { }
    }
}
