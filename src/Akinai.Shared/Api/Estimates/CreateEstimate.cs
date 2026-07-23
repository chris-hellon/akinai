using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.Estimates;

[Route("api/estimates")]
public class CreateEstimate : IPost<Guid>
{
    [BodyParam]
    public Guid ClientId { get; init; }

    [BodyParam]
    public DateOnly EstimateDate { get; init; }

    [BodyParam]
    public int ValidDays { get; init; } = 30;

    [BodyParam]
    public decimal TaxRate { get; init; }

    [BodyParam]
    public string Currency { get; init; } = "USD";

    [BodyParam]
    public string? Notes { get; init; }

    [BodyParam]
    public string? Terms { get; init; }

    [BodyParam]
    public Guid? TemplateId { get; init; }

    [BodyParam]
    public List<CreateEstimateLineItem> LineItems { get; init; } = [];
}

public class CreateEstimateLineItem
{
    public Guid? ProductId { get; init; }
    public string Description { get; init; } = null!;
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
}
