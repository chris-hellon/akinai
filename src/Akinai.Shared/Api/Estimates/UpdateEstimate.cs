using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.Estimates;

[Route("api/estimates/{Id}")]
public class UpdateEstimate : IPut
{
    [RouteParam]
    public Guid Id { get; init; }

    [BodyParam]
    public DateOnly EstimateDate { get; init; }

    [BodyParam]
    public int ValidDays { get; init; } = 30;

    [BodyParam]
    public decimal TaxRate { get; init; }

    [BodyParam]
    public string? Notes { get; init; }

    [BodyParam]
    public string? Terms { get; init; }

    [BodyParam]
    public Guid? TemplateId { get; init; }


    /// <summary>
    /// Language for this estimate, as a <c>SupportedLanguage</c> name. Null
    /// means inherit from the client override, then the account default.
    /// </summary>
    [BodyParam]
    public string? Language { get; init; }

    [BodyParam]
    public List<UpdateEstimateLineItem> LineItems { get; init; } = [];
}

public class UpdateEstimateLineItem
{
    public Guid? Id { get; init; }
    public Guid? ProductId { get; init; }
    public string Description { get; init; } = null!;
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
}
