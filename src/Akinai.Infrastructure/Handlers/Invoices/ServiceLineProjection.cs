using Akinai.Domain.Entities;
using Akinai.Shared.Dtos.Invoices;

namespace Akinai.Infrastructure.Handlers.Invoices;

/// <summary>
/// Projects a saved summary into the same shape the services table already
/// renders.
///
/// Reusing <see cref="InvoiceLineItemDto"/> rather than adding a parallel type
/// keeps one renderer, one translation path and one PDF: a summarised invoice
/// takes exactly the same route through the system as an unsummarised one,
/// differing only in how many rows it has.
///
/// Shared by the owner's view and the public view so the two cannot drift —
/// the client and the person who sent it must see the same document.
/// </summary>
internal static class ServiceLineProjection
{
    public static List<InvoiceLineItemDto> ToLineItems(
        IEnumerable<InvoiceServiceLine> serviceLines, string currency)
        => serviceLines
            .OrderBy(sl => sl.Order)
            .Select(sl => new InvoiceLineItemDto(
                sl.Id,
                // No single job underlies a group, and the field is only used
                // as a react key by the renderer.
                JobId: Guid.Empty,
                // The summary is what the client reads, so it goes where the
                // job name would — that is the column the table renders, and
                // it is what the translator picks up.
                JobName: sl.Description,
                // Deliberately blank. A group can span projects and dates, and
                // the renderer omits both when empty rather than inventing one.
                ProjectName: string.Empty,
                SectionName: null,
                Date: null,
                Description: sl.Description,
                Quantity: sl.Hours,
                UnitPrice: sl.EffectiveRate,
                LineTotal: sl.Amount,
                Currency: currency))
            .ToList();
}
