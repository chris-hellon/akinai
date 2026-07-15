using BlazorUtils.EasyApi;
using InvoiceSoftware.Shared.Api.Notifications;
using InvoiceSoftware.Shared.Dtos.Notifications;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace InvoiceSoftware.Client.Services;

/// <summary>
/// Holds the signed-in user's notification list + unread count, loads them from the API,
/// and (in the browser) keeps them live via the SignalR notifications hub. UI components
/// subscribe to <see cref="OnChange"/> and call <c>StateHasChanged</c>.
/// </summary>
public sealed class NotificationState(
    ICall<GetNotifications, NotificationListDto> getNotifications,
    ICall<MarkNotificationRead> markRead,
    ICall<MarkAllNotificationsRead> markAllRead,
    NavigationManager nav) : IAsyncDisposable
{
    private HubConnection? _hub;
    private bool _initialized;

    public List<NotificationDto> Items { get; private set; } = new();
    public int UnreadCount { get; private set; }
    public bool RealtimeConnected => _hub?.State == HubConnectionState.Connected;

    public event Action? OnChange;

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;

        await LoadAsync();

        // Real-time push only runs in the browser (WASM). During the brief
        // interactive-server phase of Interactive Auto we just use the loaded list.
        if (OperatingSystem.IsBrowser())
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
        try
        {
            _hub = new HubConnectionBuilder()
                .WithUrl(nav.ToAbsoluteUri("hubs/notifications"))
                .WithAutomaticReconnect()
                .Build();

            _hub.On<NotificationDto>("notification", dto =>
            {
                Items.Insert(0, dto);
                if (Items.Count > 50)
                    Items.RemoveRange(50, Items.Count - 50);
                if (!dto.IsRead)
                    UnreadCount++;
                OnChange?.Invoke();
            });

            await _hub.StartAsync();
            OnChange?.Invoke();
        }
        catch
        {
            // Real-time is optional; the loaded list still works and refreshes on navigation.
        }
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
        if (_hub is not null)
        {
            try { await _hub.DisposeAsync(); } catch { }
        }
    }
}
