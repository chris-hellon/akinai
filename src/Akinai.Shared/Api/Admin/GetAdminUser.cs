using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.Admin;

namespace Akinai.Shared.Api.Admin;

[Route("api/admin/users/{UserId}")]
public class GetAdminUser : IGet<AdminUserDetailDto?>
{
    [RouteParam] public string UserId { get; set; } = "";
}
