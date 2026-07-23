using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.Admin;

/// <summary>Force-confirm an email when a user never received the link.</summary>
[Route("api/admin/users/{UserId}/confirm-email")]
public class ConfirmUserEmail : IPost
{
    [RouteParam] public string UserId { get; set; } = "";
}
