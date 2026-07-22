using BlazorUtils.EasyApi;

namespace InvoiceSoftware.Shared.Api.Admin;

/// <summary>Grant or revoke the Admin role.</summary>
[Route("api/admin/users/{UserId}/admin")]
public class SetUserAdmin : IPost
{
    [RouteParam] public string UserId { get; set; } = "";
    [BodyParam] public bool IsAdmin { get; set; }
}
