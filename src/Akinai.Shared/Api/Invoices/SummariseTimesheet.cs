using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.Invoices;

namespace Akinai.Shared.Api.Invoices;

/// <summary>
/// Proposes how this invoice's tracked time should read to the client.
///
/// A preview: nothing is written. The user reviews the grouping, edits the
/// wording, and saves it with <see cref="SaveInvoiceServiceLines"/> — or walks
/// away and keeps the raw rows.
/// </summary>
[Route("api/invoices/{Id}/summarise")]
public class SummariseTimesheet : IPost<TimesheetSummaryDto>
{
    [RouteParam] public Guid Id { get; set; }

    /// <summary>
    /// How the descriptions should read — see <see cref="Akinai.Shared.Ai.SummaryTone"/>.
    /// Anything unrecognised falls back to Professional server-side.
    /// </summary>
    [BodyParam] public string Tone { get; set; } = Akinai.Shared.Ai.SummaryTone.Professional;
}

/// <summary>
/// Stores a reviewed summary, replacing whatever was there.
///
/// Only the wording travels. The hours and amounts are re-derived on the server
/// from the grouped entries, so an edited description cannot change what the
/// client owes — and a crafted request cannot either.
/// </summary>
[Route("api/invoices/{Id}/service-lines")]
public class SaveInvoiceServiceLines : IPut
{
    [RouteParam] public Guid Id { get; set; }

    [BodyParam] public List<ServiceLineInput> Lines { get; set; } = [];
}

/// <summary>Drops the summary and goes back to one row per time entry.</summary>
[Route("api/invoices/{Id}/service-lines/clear")]
public class ClearInvoiceServiceLines : IPost
{
    [RouteParam] public Guid Id { get; set; }
}

/// <summary>One reviewed group on the way back to the server.</summary>
public class ServiceLineInput
{
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The entries in this group. The server re-checks that these cover every
    /// billable entry on the invoice exactly once before saving anything.
    /// </summary>
    public List<Guid> TimeEntryIds { get; set; } = [];
}
