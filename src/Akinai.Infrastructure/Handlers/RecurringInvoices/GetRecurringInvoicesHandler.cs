using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api;
using Akinai.Shared.Api.RecurringInvoices;
using Akinai.Shared.Dtos.RecurringInvoices;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.RecurringInvoices;

public class GetRecurringInvoicesHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService)
    : IHandle<GetRecurringInvoices, PaginatedResponse<RecurringInvoiceSummaryDto>>
{
    public async Task<HttpResult<PaginatedResponse<RecurringInvoiceSummaryDto>>> Handle(
        GetRecurringInvoices request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<PaginatedResponse<RecurringInvoiceSummaryDto>>.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.RecurringInvoices
            .Include(r => r.Client)
            .Where(r => r.UserId == userId);

        if (request.ActiveOnly)
            query = query.Where(r => r.IsActive);

        if (request.ClientId.HasValue)
            query = query.Where(r => r.ClientId == request.ClientId.Value);

        if (!string.IsNullOrEmpty(request.Search))
            query = query.Where(r => r.TemplateName.Contains(request.Search) || r.Client.Name.Contains(request.Search));

        var totalCount = await query.CountAsync(cancellationToken);

        var recurringInvoices = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Include(r => r.LineItems)
            .ToListAsync(cancellationToken);

        var items = recurringInvoices.Select(r =>
        {
            var estimatedTotal = r.LineItems.Sum(li => li.LineTotal.Amount);
            estimatedTotal += estimatedTotal * r.TaxRate / 100;

            return new RecurringInvoiceSummaryDto(
                r.Id,
                r.ClientId,
                r.Client.Name,
                r.TemplateName,
                r.Currency,
                r.GetFrequencyDescription(),
                r.NextInvoiceDate,
                r.IsActive,
                r.GeneratedCount,
                estimatedTotal);
        }).ToList();

        return HttpResult<PaginatedResponse<RecurringInvoiceSummaryDto>>.Ok(
            new PaginatedResponse<RecurringInvoiceSummaryDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = request.Page,
                PageSize = request.PageSize
            });
    }
}
