using BlazorUtils.EasyApi;

namespace InvoiceSoftware.Shared.Api.Notifications;

[Route("api/notifications/{Id}/read")]
public class MarkNotificationRead : IPost
{
    [RouteParam]
    public Guid Id { get; init; }
}
