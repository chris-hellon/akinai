using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.Search;

namespace Akinai.Shared.Api.Search;

[Route("api/search")]
public class GlobalSearch : IGet<GlobalSearchResultDto>
{
    [QueryStringParam]
    public string Query { get; set; } = "";
}
