using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.RecurringInvoices;

[Route("api/recurring-invoices")]
public class CreateRecurringInvoice : IPost<Guid>
{
    [BodyParam]
    public Guid ClientId { get; init; }

    [BodyParam]
    public string TemplateName { get; init; } = null!;

    [BodyParam]
    public string Currency { get; init; } = "USD";

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
    public List<CreateRecurringInvoiceLineItem> LineItems { get; init; } = [];
}

public class CreateRecurringInvoiceLineItem
{
    public Guid? ProductId { get; init; }
    public string Description { get; init; } = null!;
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
}
