using BlazorUtils.EasyApi;
using InvoiceSoftware.Shared.Dtos.Search;

namespace InvoiceSoftware.Shared.Api.Search;

[Route("api/search")]
public class GlobalSearch : IGet<GlobalSearchResultDto>
{
    [QueryStringParam]
    public string Query { get; set; } = "";
}
