using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.Invoices;

namespace Akinai.Shared.Api.Invoices;

[Route("api/invoices")]
public class CreateInvoice : IPost<Guid>
{
    [BodyParam]
    public Guid ClientId { get; init; }

    [BodyParam]
    public List<Guid> TimeEntryIds { get; init; } = [];

    [BodyParam]
    public List<Guid> ExpenseIds { get; init; } = [];

    [BodyParam]
    public List<ProductLineItemRequest> ProductLineItems { get; init; } = [];

    [BodyParam]
    public DateOnly IssueDate { get; init; }

    [BodyParam]
    public DateOnly DueDate { get; init; }

    [BodyParam]
    public decimal TaxRate { get; init; }

    [BodyParam]
    public string? Notes { get; init; }

    [BodyParam]
    public Guid? TemplateId { get; init; }

    /// <summary>
    /// Language for this invoice, as a <c>SupportedLanguage</c> name. Null means
    /// inherit from the client override, then the account default.
    /// </summary>
    [BodyParam]
    public string? Language { get; init; }
}
