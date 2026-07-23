using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.Notifications;

[Route("api/notifications/{Id}/read")]
public class MarkNotificationRead : IPost
{
    [RouteParam]
    public Guid Id { get; init; }
}
