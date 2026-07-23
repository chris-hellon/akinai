using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.Admin;

/// <summary>Lock an account out (indefinitely) or lift an existing lockout.</summary>
[Route("api/admin/users/{UserId}/lockout")]
public class SetUserLockout : IPost
{
    [RouteParam] public string UserId { get; set; } = "";
    [BodyParam] public bool LockedOut { get; set; }
}
