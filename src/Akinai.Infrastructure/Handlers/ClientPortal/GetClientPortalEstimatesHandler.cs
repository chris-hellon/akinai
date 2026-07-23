using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.Enums;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api;
using Akinai.Shared.Api.ClientPortal;
using Akinai.Shared.Dtos.ClientPortal;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.ClientPortal;

public class GetClientPortalEstimatesHandler(IDbContextFactory<ApplicationDbContext> dbFactory)
    : IHandle<GetClientPortalEstimates, PaginatedResponse<ClientPortalEstimateDto>>
{
    public async Task<HttpResult<PaginatedResponse<ClientPortalEstimateDto>>> Handle(
        GetClientPortalEstimates request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Find client by portal token
        var client = await db.Clients
            .FirstOrDefaultAsync(c => c.PortalToken == request.ClientToken, cancellationToken);

        if (client == null)
            return HttpResult<PaginatedResponse<ClientPortalEstimateDto>>.NotFound();

        var query = db.Estimates
            .Include(e => e.LineItems)
            .Where(e => e.ClientId == client.Id);

        // Only show sent, accepted, rejected estimates (not drafts)
        query = query.Where(e =>
            e.Status == EstimateStatus.Sent ||
            e.Status == EstimateStatus.Accepted ||
            e.Status == EstimateStatus.Rejected ||
            e.Status == EstimateStatus.Converted);

        if (request.Status != null && Enum.TryParse<EstimateStatus>(request.Status, out var status))
        {
            query = query.Where(e => e.Status == status);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var estimates = await query
            .OrderByDescending(e => e.EstimateDate)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var items = estimates.Select(e =>
        {
            var canAccept = e.Status == EstimateStatus.Sent &&
                           (!e.ExpiryDate.HasValue || e.ExpiryDate.Value >= DateOnly.FromDateTime(DateTime.Today));
            var canReject = e.Status == EstimateStatus.Sent;

            return new ClientPortalEstimateDto(
                e.Id,
                e.EstimateNumber,
                e.EstimateDate,
                e.ExpiryDate,
                e.Status.ToString(),
                e.CalculateTotal().Amount,
                e.Currency,
                e.AcceptedDate,
                e.RejectedDate,
                e.PublicAccessToken,
                canAccept,
                canReject);
        }).ToList();

        return HttpResult<PaginatedResponse<ClientPortalEstimateDto>>.Ok(
            new PaginatedResponse<ClientPortalEstimateDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = request.Page,
                PageSize = request.PageSize
            });
    }
}
