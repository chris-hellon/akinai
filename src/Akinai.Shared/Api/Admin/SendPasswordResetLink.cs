using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.Admin;

[Route("api/admin/users/{UserId}/password-reset")]
public class SendPasswordResetLink : IPost
{
    [RouteParam] public string UserId { get; set; } = "";
}
