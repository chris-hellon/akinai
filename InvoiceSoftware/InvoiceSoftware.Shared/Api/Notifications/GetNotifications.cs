using BlazorUtils.EasyApi;
using InvoiceSoftware.Shared.Dtos.Notifications;

namespace InvoiceSoftware.Shared.Api.Notifications;

[Route("api/notifications")]
public class GetNotifications : IGet<NotificationListDto>
{
    [QueryStringParam]
    public int Take { get; set; } = 20;

    [QueryStringParam]
    public bool UnreadOnly { get; set; }
}
