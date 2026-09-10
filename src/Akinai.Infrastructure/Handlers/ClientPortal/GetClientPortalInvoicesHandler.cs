using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.Enums;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api;
using Akinai.Shared.Api.ClientPortal;
using Akinai.Shared.Dtos.ClientPortal;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.ClientPortal;

public class GetClientPortalInvoicesHandler(IDbContextFactory<ApplicationDbContext> dbFactory)
    : IHandle<GetClientPortalInvoices, PaginatedResponse<ClientPortalInvoiceDto>>
{
    public async Task<HttpResult<PaginatedResponse<ClientPortalInvoiceDto>>> Handle(
        GetClientPortalInvoices request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Find client by portal token
        var client = await db.Clients
            .FirstOrDefaultAsync(c => c.PortalToken == request.ClientToken, cancellationToken);

        if (client == null)
            return HttpResult<PaginatedResponse<ClientPortalInvoiceDto>>.NotFound();

        var query = db.Invoices
            .Where(i => i.ClientId == client.Id);

        // Only show sent, overdue, and paid invoices (not drafts)
        query = query.Where(i =>
            i.Status == InvoiceStatus.Sent ||
            i.Status == InvoiceStatus.Overdue ||
            i.Status == InvoiceStatus.Paid);

        if (request.Status != null && Enum.TryParse<InvoiceStatus>(request.Status, out var status))
        {
            query = query.Where(i => i.Status == status);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var invoices = await query
            .OrderByDescending(i => i.IssueDate)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var items = invoices.Select(i =>
        {
            // The stored total — the figure the client was actually invoiced. Deriving
            // it here from raw hours understated it: it missed the quarter-hour billing
            // rounding, and counted neither expenses nor product line items.
            var total = i.Total;

            return new ClientPortalInvoiceDto(
                i.Id,
                i.InvoiceNumber,
                i.IssueDate,
                i.DueDate,
                i.Status.ToString(),
                total,
                i.Currency,
                i.PaidDate,
                i.PublicAccessToken);
        }).ToList();

        return HttpResult<PaginatedResponse<ClientPortalInvoiceDto>>.Ok(
            new PaginatedResponse<ClientPortalInvoiceDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = request.Page,
                PageSize = request.PageSize
            });
    }
}
