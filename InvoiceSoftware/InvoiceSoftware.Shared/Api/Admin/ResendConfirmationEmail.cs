using BlazorUtils.EasyApi;

namespace InvoiceSoftware.Shared.Api.Admin;

[Route("api/admin/users/{UserId}/resend-confirmation")]
public class ResendConfirmationEmail : IPost
{
    [RouteParam] public string UserId { get; set; } = "";
}
