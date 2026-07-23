using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.TimeSuggestions;

namespace Akinai.Shared.Api.TimeSuggestions;

[Route("api/time-suggestions")]
public class GetTimeSuggestions : IGet<List<TimeSuggestionDto>>
{
    [QueryStringParam]
    public Guid? ClientId { get; init; }

    [QueryStringParam]
    public Guid? ProjectId { get; init; }

    [QueryStringParam]
    public Guid? JobId { get; init; }

    [QueryStringParam]
    public int Limit { get; init; } = 5;
}
