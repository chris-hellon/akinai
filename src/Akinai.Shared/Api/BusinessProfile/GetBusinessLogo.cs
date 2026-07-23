using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.BusinessProfile;

[Route("api/business-profile/logo")]
public class GetBusinessLogo : IGet<byte[]?>
{
}
