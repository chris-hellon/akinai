using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.Account;

[Route("api/account/delete")]
public class DeleteAccount : IPost
{
    [BodyParam]
    public string Password { get; set; } = "";
}
