using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.Account;

[Route("api/account/email/change")]
public class ChangeEmail : IPost
{
    [BodyParam]
    public string NewEmail { get; set; } = "";
}
