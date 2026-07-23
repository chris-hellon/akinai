using BlazorUtils.EasyApi;
using InvoiceSoftware.Shared.Dtos.Invoices;

namespace InvoiceSoftware.Shared.Api.Invoices;

/// <summary>
/// Gets an invoice by its public access token. No authentication required.
/// </summary>
[Route("/api/invoices/public/{Token}")]
public class GetPublicInvoice : IGet<PublicInvoiceDto?>
{
    [RouteParam]
    public Guid Token { get; set; }

    /// <summary>
    /// Count this request as the client viewing the invoice.
    ///
    /// Off by default, and that default is the point: the PDF renderer loads
    /// the same invoice through this contract, so a counter that opted in
    /// automatically would tick up every time the owner downloaded a PDF.
    /// Only the public web page asks for it.
    /// </summary>
    [QueryStringParam]
    public bool RecordView { get; init; }
}
