using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.TimeEntries;

[Route("api/timeentries")]
public class LogTimeEntry : IPost<Guid>
{
    [BodyParam]
    public Guid JobId { get; init; }

    [BodyParam]
    public DateOnly Date { get; init; }

    [BodyParam]
    public decimal Hours { get; init; }

    [BodyParam]
    public string? Description { get; init; }
}
