using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.Enums;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api;
using Akinai.Shared.Api.Invoices;
using Akinai.Shared.Dtos.Invoices;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Invoices;

public class GetInvoicesHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService) : IHandle<GetInvoices, PaginatedResponse<InvoiceSummaryDto>>
{
    public async Task<HttpResult<PaginatedResponse<InvoiceSummaryDto>>> Handle(GetInvoices request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult<PaginatedResponse<InvoiceSummaryDto>>.Unauthorized();

        var query = db.Invoices
            .Include(i => i.Client)
            .Where(i => i.Client.UserId == userId);

        // Status filter
        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<InvoiceStatus>(request.Status, out var status))
            query = query.Where(i => i.Status == status);

        // Search filter
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(i =>
                i.InvoiceNumber.ToLower().Contains(search) ||
                i.Client.Name.ToLower().Contains(search));
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply pagination
        var invoices = await query
            .OrderByDescending(i => i.IssueDate)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        // Totals come from the snapshot on the invoice, written by IInvoiceTotalService
        // on create/update. Recomputing them per row meant three extra queries — one of
        // them a four-level Include — for every invoice on the page.
        var items = invoices.Select(invoice => new InvoiceSummaryDto(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.ClientId,
            invoice.Client.Name,
            invoice.Currency,
            invoice.IssueDate,
            invoice.DueDate,
            invoice.Status.ToString(),
            invoice.Subtotal,
            invoice.TaxAmount,
            invoice.Total,
            invoice.SentAtUtc)).ToList();

        var result = new PaginatedResponse<InvoiceSummaryDto>
        {
            Items = items,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount
        };

        return HttpResult<PaginatedResponse<InvoiceSummaryDto>>.Ok(result);
    }
}
