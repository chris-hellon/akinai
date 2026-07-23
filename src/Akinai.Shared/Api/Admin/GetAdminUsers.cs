using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.Admin;

namespace Akinai.Shared.Api.Admin;

[Route("api/admin/users")]
public class GetAdminUsers : IGet<PaginatedResponse<AdminUserDto>>, IPaginatedRequest
{
    [QueryStringParam] public int Page { get; set; } = 1;
    [QueryStringParam] public int PageSize { get; set; } = 25;
    [QueryStringParam] public string? Search { get; set; }
}
