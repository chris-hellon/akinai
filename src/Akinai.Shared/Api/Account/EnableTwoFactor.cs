using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.Account;

namespace Akinai.Shared.Api.Account;

[Route("api/account/2fa/enable")]
public class EnableTwoFactor : IPost<EnableTwoFactorResultDto>
{
    [BodyParam]
    public string Code { get; set; } = "";
}
