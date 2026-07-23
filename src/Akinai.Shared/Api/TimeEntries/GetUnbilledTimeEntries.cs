using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.TimeEntries;

namespace Akinai.Shared.Api.TimeEntries;

[Route("api/timeentries/unbilled/{ClientId}")]
public class GetUnbilledTimeEntries : IGet<List<UnbilledTimeEntryDto>>
{
    [RouteParam]
    public Guid ClientId { get; init; }
}
