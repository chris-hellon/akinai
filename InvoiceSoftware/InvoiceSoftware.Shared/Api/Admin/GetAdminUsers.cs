using BlazorUtils.EasyApi;
using InvoiceSoftware.Shared.Api;
using InvoiceSoftware.Shared.Dtos.Admin;

namespace InvoiceSoftware.Shared.Api.Admin;

[Route("api/admin/users")]
public class GetAdminUsers : IGet<PaginatedResponse<AdminUserDto>>, IPaginatedRequest
{
    [QueryStringParam] public int Page { get; set; } = 1;
    [QueryStringParam] public int PageSize { get; set; } = 25;
    [QueryStringParam] public string? Search { get; set; }
}
