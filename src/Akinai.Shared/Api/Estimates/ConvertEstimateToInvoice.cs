using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.Estimates;

[Route("api/estimates/{Id}/convert")]
public class ConvertEstimateToInvoice : IPost<Guid>
{
    [RouteParam]
    public Guid Id { get; init; }

    [BodyParam]
    public DateOnly? IssueDate { get; init; }

    [BodyParam]
    public int DueDays { get; init; } = 30;
}
