using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace InvoiceSoftware.Infrastructure.Hubs;

/// <summary>
/// Real-time channel for user notifications. Clients connect authenticated (cookie auth),
/// and the server pushes to a specific user via <c>Clients.User(userId)</c> — SignalR's
/// default <see cref="IUserIdProvider"/> maps that to the NameIdentifier claim, which is
/// the same id used throughout the app (see <c>ICurrentUserService.UserId</c>).
/// The server pushes; clients only receive, so the hub itself needs no methods.
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
}
