using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.Account;

[Route("api/account/change-password")]
public class ChangePassword : IPost
{
    [BodyParam]
    public string CurrentPassword { get; set; } = "";
    [BodyParam]
    public string NewPassword { get; set; } = "";
}
