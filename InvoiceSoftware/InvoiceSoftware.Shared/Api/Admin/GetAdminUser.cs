using BlazorUtils.EasyApi;
using InvoiceSoftware.Shared.Dtos.Admin;

namespace InvoiceSoftware.Shared.Api.Admin;

[Route("api/admin/users/{UserId}")]
public class GetAdminUser : IGet<AdminUserDetailDto?>
{
    [RouteParam] public string UserId { get; set; } = "";
}
