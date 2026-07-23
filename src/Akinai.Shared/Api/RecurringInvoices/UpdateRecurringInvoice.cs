using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.RecurringInvoices;

[Route("api/recurring-invoices/{Id}")]
public class UpdateRecurringInvoice : IPut
{
    [RouteParam]
    public Guid Id { get; init; }

    [BodyParam]
    public string TemplateName { get; init; } = null!;

    [BodyParam]
    public int FrequencyInterval { get; init; } = 1;

    [BodyParam]
    public string Frequency { get; init; } = "Month";

    [BodyParam]
    public DateOnly StartDate { get; init; }

    [BodyParam]
    public DateOnly? EndDate { get; init; }

    [BodyParam]
    public int DueDays { get; init; } = 30;

    [BodyParam]
    public decimal TaxRate { get; init; }

    [BodyParam]
    public string? Notes { get; init; }

    [BodyParam]
    public string? Terms { get; init; }

    [BodyParam]
    public string? Footer { get; init; }

    [BodyParam]
    public List<UpdateRecurringInvoiceLineItem> LineItems { get; init; } = [];
}

public class UpdateRecurringInvoiceLineItem
{
    public Guid? Id { get; init; }
    public Guid? ProductId { get; init; }
    public string Description { get; init; } = null!;
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
}
