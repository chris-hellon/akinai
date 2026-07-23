using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.BusinessProfile;

[Route("api/business-profile/logo")]
public class SaveBusinessLogo : IPost
{
    [BodyParam]
    public byte[] Logo { get; init; } = null!;

    [BodyParam]
    public string ContentType { get; init; } = null!;
}
