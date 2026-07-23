using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.Enums;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api;
using Akinai.Shared.Api.Estimates;
using Akinai.Shared.Dtos.Estimates;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Estimates;

public class GetEstimatesHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService)
    : IHandle<GetEstimates, PaginatedResponse<EstimateSummaryDto>>
{
    public async Task<HttpResult<PaginatedResponse<EstimateSummaryDto>>> Handle(GetEstimates request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<PaginatedResponse<EstimateSummaryDto>>.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Estimates
            .Include(e => e.Client)
            .Include(e => e.LineItems)
            .Where(e => e.UserId == userId);

        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<EstimateStatus>(request.Status, out var status))
            query = query.Where(e => e.Status == status);

        if (request.ClientId.HasValue)
            query = query.Where(e => e.ClientId == request.ClientId.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(e =>
                e.EstimateNumber.ToLower().Contains(search) ||
                e.Client.Name.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var estimates = await query
            .OrderByDescending(e => e.EstimateDate)
            .ThenBy(e => e.EstimateNumber)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var items = estimates.Select(e =>
        {
            var subtotal = e.CalculateSubtotal().Amount;
            var taxAmount = e.CalculateTax().Amount;
            var total = e.CalculateTotal().Amount;

            return new EstimateSummaryDto(
                e.Id,
                e.EstimateNumber,
                e.ClientId,
                e.Client.Name,
                e.Currency,
                e.EstimateDate,
                e.ExpiryDate,
                e.Status.ToString(),
                subtotal,
                taxAmount,
                total);
        }).ToList();

        var result = new PaginatedResponse<EstimateSummaryDto>
        {
            Items = items,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount
        };

        return HttpResult<PaginatedResponse<EstimateSummaryDto>>.Ok(result);
    }
}
