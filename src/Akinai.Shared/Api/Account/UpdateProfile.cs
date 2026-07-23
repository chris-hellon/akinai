using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.Account;

[Route("api/account/profile")]
public class UpdateProfile : IPut
{
    [BodyParam]
    public string? PhoneNumber { get; set; }
}
