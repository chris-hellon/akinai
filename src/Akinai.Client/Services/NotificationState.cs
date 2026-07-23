using BlazorUtils.EasyApi;
using Akinai.Shared.Api.Notifications;
using Akinai.Shared.Dtos.Notifications;

namespace Akinai.Client.Services;

/// <summary>
/// Holds the signed-in user's notification list + unread count, loads them from the API,
/// and keeps them live through <see cref="IRealtimeUserChannel"/>. UI components
/// subscribe to <see cref="OnChange"/> and call <c>StateHasChanged</c>.
///
/// Which realtime transport that is depends on where the page is running — see
/// the interface. This class does not care, which is the point: it used to skip
/// realtime entirely whenever it was not on WebAssembly.
/// </summary>
public sealed class NotificationState(
    ICall<GetNotifications, NotificationListDto> getNotifications,
    ICall<MarkNotificationRead> markRead,
    ICall<MarkAllNotificationsRead> markAllRead,
    EntitlementState entitlements,
    IRealtimeUserChannel realtime) : IAsyncDisposable
{
    private bool _initialized;

    public List<NotificationDto> Items { get; private set; } = new();
    public int UnreadCount { get; private set; }
    public bool RealtimeConnected => realtime.IsConnected;

    public event Action? OnChange;

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;

        await LoadAsync();

        await ConnectAsync();
    }

    public async Task LoadAsync()
    {
        try
        {
            var result = await getNotifications.Call(new GetNotifications { Take = 30 });
            if (result is not null)
            {
                Items = result.Items ?? new();
                UnreadCount = result.UnreadCount;
                OnChange?.Invoke();
            }
        }
        catch
        {
            // Keep whatever we already have.
        }
    }

    private async Task ConnectAsync()
    {
        await realtime.ConnectAsync(
            onNotification: dto =>
            {
                Items.Insert(0, dto);
                if (Items.Count > 50)
                    Items.RemoveRange(50, Items.Count - 50);
                if (!dto.IsRead)
                    UnreadCount++;
                OnChange?.Invoke();
            },
            // No payload: it only says "re-read what you're allowed to do".
            onEntitlementsChanged: () => _ = entitlements.ReloadAsync());

        OnChange?.Invoke();
    }

    public async Task MarkAsReadAsync(Guid id)
    {
        var item = Items.FirstOrDefault(n => n.Id == id);
        if (item is null || item.IsRead) return;

        item.IsRead = true;
        if (UnreadCount > 0) UnreadCount--;
        OnChange?.Invoke();

        try { await markRead.Call(new MarkNotificationRead { Id = id }); } catch { }
    }

    public async Task MarkAllAsReadAsync()
    {
        if (UnreadCount == 0) return;

        foreach (var n in Items) n.IsRead = true;
        UnreadCount = 0;
        OnChange?.Invoke();

        try { await markAllRead.Call(new MarkAllNotificationsRead()); } catch { }
    }

    public async ValueTask DisposeAsync()
    {
        await realtime.DisposeAsync();
    }
}
