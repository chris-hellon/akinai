using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.Admin;

namespace Akinai.Shared.Api.Admin;

/// <summary>
/// Creates an account with no password and emails a set-password link.
/// The admin never handles the user's credential.
/// </summary>
[Route("api/admin/users/invite")]
public class InviteUser : IPost<InviteResultDto>
{
    [BodyParam] public string Email { get; set; } = "";
}
