using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.Enums;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api;
using Akinai.Shared.Api.ClientPortal;
using Akinai.Shared.Dtos.ClientPortal;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.ClientPortal;

public class GetClientPortalPaymentHistoryHandler(IDbContextFactory<ApplicationDbContext> dbFactory)
    : IHandle<GetClientPortalPaymentHistory, PaginatedResponse<PaymentHistoryDto>>
{
    public async Task<HttpResult<PaginatedResponse<PaymentHistoryDto>>> Handle(
        GetClientPortalPaymentHistory request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Find client by portal token
        var client = await db.Clients
            .FirstOrDefaultAsync(c => c.PortalToken == request.ClientToken, cancellationToken);

        if (client == null)
            return HttpResult<PaginatedResponse<PaymentHistoryDto>>.NotFound();

        // Get paid invoices as payment history
        var query = db.Invoices
            .Include(i => i.TimeEntries)
                .ThenInclude(te => te.Job)
                    .ThenInclude(j => j.Project)
                        .ThenInclude(p => p.Client)
            .Where(i => i.ClientId == client.Id && i.Status == InvoiceStatus.Paid && i.PaidDate != null);

        var totalCount = await query.CountAsync(cancellationToken);

        var invoices = await query
            .OrderByDescending(i => i.PaidDate)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var items = invoices.Select(i =>
        {
            decimal subtotal = 0;
            foreach (var te in i.TimeEntries)
            {
                var hourlyRate = te.Job.GetEffectiveHourlyRate();
                subtotal += te.Hours.Value * hourlyRate;
            }
            var tax = subtotal * i.TaxRate / 100;
            var total = subtotal + tax;

            return new PaymentHistoryDto(
                i.Id,
                i.InvoiceNumber,
                i.PaidDate!.Value,
                total,
                i.Currency);
        }).ToList();

        return HttpResult<PaginatedResponse<PaymentHistoryDto>>.Ok(
            new PaginatedResponse<PaymentHistoryDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = request.Page,
                PageSize = request.PageSize
            });
    }
}
