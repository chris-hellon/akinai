using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.Evidence;

namespace Akinai.Shared.Api.Invoices;

[Route("api/invoices/{Id}/evidence")]
public class GetInvoiceEvidence : IGet<InvoiceEvidenceDto?>
{
    [RouteParam]
    public Guid Id { get; init; }
}
