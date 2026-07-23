using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.Invoices;

namespace Akinai.Shared.Api.Invoices;

[Route("api/invoices/{Id}")]
public class UpdateInvoice : IPut
{
    [RouteParam]
    public Guid Id { get; init; }

    [BodyParam]
    public DateOnly IssueDate { get; init; }

    [BodyParam]
    public DateOnly DueDate { get; init; }

    [BodyParam]
    public decimal TaxRate { get; init; }

    [BodyParam]
    public string? Notes { get; init; }

    [BodyParam]
    public List<Guid>? AddedTimeEntryIds { get; init; }

    [BodyParam]
    public List<Guid>? RemovedTimeEntryIds { get; init; }

    [BodyParam]
    public List<Guid>? AddedExpenseIds { get; init; }

    [BodyParam]
    public List<Guid>? RemovedExpenseIds { get; init; }

    [BodyParam]
    public List<ProductLineItemRequest>? ProductLineItems { get; init; }

    /// <summary>
    /// Language for this invoice, as a <c>SupportedLanguage</c> name. Null means
    /// inherit from the client override, then the account default.
    /// </summary>
    [BodyParam]
    public string? Language { get; init; }
}
