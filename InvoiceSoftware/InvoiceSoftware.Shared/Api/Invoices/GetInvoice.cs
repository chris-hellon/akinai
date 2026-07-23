using BlazorUtils.EasyApi;
using InvoiceSoftware.Shared.Dtos.Invoices;

namespace InvoiceSoftware.Shared.Api.Invoices;

[Route("api/invoices/{Id}")]
public class GetInvoice : IGet<InvoiceDetailDto?>
{
    [RouteParam]
    public Guid Id { get; init; }

    /// <summary>
    /// Return the invoice as the client will see it — freeform text translated
    /// into the invoice's language.
    ///
    /// Off by default, and that default matters: the edit form loads through
    /// this same contract, and handing it translated descriptions would let a
    /// save quietly overwrite the user's original English. Only the read-only
    /// detail view asks for this.
    /// </summary>
    [QueryStringParam]
    public bool Translated { get; init; }
}
