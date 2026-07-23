using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.BusinessProfile;

namespace Akinai.Shared.Api.BusinessProfile;

[Route("api/business-profile")]
public class GetBusinessProfile : IGet<BusinessProfileDto?>
{
}
