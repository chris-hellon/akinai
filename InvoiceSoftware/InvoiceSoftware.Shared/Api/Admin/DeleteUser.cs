using BlazorUtils.EasyApi;

namespace InvoiceSoftware.Shared.Api.Admin;

/// <summary>
/// Permanently erases an account and everything it owns (GDPR erasure).
/// Irreversible. <see cref="ConfirmEmail"/> must match the account's address —
/// a deliberate speed bump so this can't fire from a stray click.
/// </summary>
[Route("api/admin/users/{UserId}/delete")]
public class DeleteUser : IPost
{
    [RouteParam] public string UserId { get; set; } = "";
    [BodyParam] public string ConfirmEmail { get; set; } = "";
}
