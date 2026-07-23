using Akinai.Shared.Dtos.Notifications;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace Akinai.Client.Services;

/// <summary>
/// Realtime over SignalR, for pages running on WebAssembly.
///
/// The browser holds the connection and authenticates with the same cookie it
/// uses for the API, so the hub can address this user with Clients.User(...).
/// </summary>
public sealed class SignalRUserChannel(NavigationManager nav) : IRealtimeUserChannel
{
    private HubConnection? _hub;

    public bool IsConnected => _hub?.State == HubConnectionState.Connected;

    public async Task ConnectAsync(
        Action<NotificationDto> onNotification,
        Action onEntitlementsChanged,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _hub = new HubConnectionBuilder()
                .WithUrl(nav.ToAbsoluteUri("hubs/notifications"))
                .WithAutomaticReconnect()
                .Build();

            _hub.On<NotificationDto>("notification", onNotification);
            _hub.On("entitlements", onEntitlementsChanged);

            await _hub.StartAsync(cancellationToken);
        }
        catch
        {
            // Realtime is an enhancement, never a requirement: the list still
            // loads over HTTP and refreshes on navigation.
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hub is null) return;
        try { await _hub.DisposeAsync(); } catch { }
    }
}
