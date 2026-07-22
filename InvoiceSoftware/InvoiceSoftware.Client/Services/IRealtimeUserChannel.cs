using InvoiceSoftware.Shared.Dtos.Notifications;

namespace InvoiceSoftware.Client.Services;

/// <summary>
/// How live events reach the signed-in user, whichever runtime the page is on.
///
/// This exists because the app renders with Interactive Auto, so the same
/// component runs in two very different places. On WebAssembly the browser can
/// hold a SignalR connection to the hub. On Blazor Server it cannot — the
/// component is already running on the server, and a hub connection from there
/// would have no way to present the user's auth cookie. Before this abstraction
/// the client simply skipped realtime whenever it was on Server, which meant
/// notifications silently never arrived until the next page load.
///
/// Two implementations, chosen by which host registers them:
/// WASM gets the SignalR client; Blazor Server gets an in-process subscription
/// to the same events the hub would have carried.
/// </summary>
public interface IRealtimeUserChannel : IAsyncDisposable
{
    bool IsConnected { get; }

    /// <summary>
    /// Starts listening. Both callbacks are invoked on an arbitrary thread —
    /// callers are expected to marshal onto the renderer themselves.
    /// </summary>
    Task ConnectAsync(
        Action<NotificationDto> onNotification,
        Action onEntitlementsChanged,
        CancellationToken cancellationToken = default);
}
