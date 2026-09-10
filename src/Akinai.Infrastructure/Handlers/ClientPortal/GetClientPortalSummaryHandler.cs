using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.Enums;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.ClientPortal;
using Akinai.Shared.Dtos.ClientPortal;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.ClientPortal;

public class GetClientPortalSummaryHandler(IDbContextFactory<ApplicationDbContext> dbFactory)
    : IHandle<GetClientPortalSummary, ClientPortalSummaryDto?>
{
    public async Task<HttpResult<ClientPortalSummaryDto?>> Handle(
        GetClientPortalSummary request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Find client by portal token
        var client = await db.Clients
            .FirstOrDefaultAsync(c => c.PortalToken == request.Token, cancellationToken);

        if (client == null)
            return HttpResult<ClientPortalSummaryDto?>.NotFound();

        // Get business profile
        var businessProfile = await db.BusinessProfiles
            .FirstOrDefaultAsync(bp => bp.UserId == client.UserId, cancellationToken);

        // Get invoices for this client
        var invoices = await db.Invoices
            .Where(i => i.ClientId == client.Id &&
                       (i.Status == InvoiceStatus.Sent ||
                        i.Status == InvoiceStatus.Overdue ||
                        i.Status == InvoiceStatus.Paid))
            .ToListAsync(cancellationToken);

        var totalInvoices = invoices.Count;
        var unpaidInvoices = invoices.Count(i => i.Status != InvoiceStatus.Paid);

        // Calculate outstanding amount
        // Stored totals — what the client was actually invoiced. Deriving them here
        // from raw hours understated the balance: no quarter-hour billing rounding,
        // and neither expenses nor product line items counted.
        decimal totalOutstanding = invoices
            .Where(i => i.Status != InvoiceStatus.Paid)
            .Sum(i => i.Total);

        // Get pending estimates
        var pendingEstimates = await db.Estimates
            .Where(e => e.ClientId == client.Id && e.Status == EstimateStatus.Sent)
            .CountAsync(cancellationToken);

        var dto = new ClientPortalSummaryDto(
            client.Name,
            client.CompanyName,
            businessProfile?.CompanyName ?? "Your Business",
            businessProfile?.Logo,
            businessProfile?.LogoContentType,
            totalInvoices,
            unpaidInvoices,
            totalOutstanding,
            client.Currency,
            pendingEstimates);

        return HttpResult<ClientPortalSummaryDto?>.Ok(dto);
    }
}
