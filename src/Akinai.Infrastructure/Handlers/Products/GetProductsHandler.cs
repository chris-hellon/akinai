using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api;
using Akinai.Shared.Api.Products;
using Akinai.Shared.Dtos.Products;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Products;

public class GetProductsHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService)
    : IHandle<GetProducts, PaginatedResponse<ProductDto>>
{
    public async Task<HttpResult<PaginatedResponse<ProductDto>>> Handle(GetProducts request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<PaginatedResponse<ProductDto>>.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Products.Where(p => p.UserId == userId);

        if (request.ActiveOnly)
            query = query.Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(request.Category))
            query = query.Where(p => p.Category == request.Category);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(p =>
                p.Name.ToLower().Contains(search) ||
                (p.Description != null && p.Description.ToLower().Contains(search)) ||
                (p.Sku != null && p.Sku.ToLower().Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var products = await query
            .OrderBy(p => p.Name)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var items = products.Select(p => new ProductDto(
            p.Id,
            p.Name,
            p.Description,
            p.UnitPrice.Amount,
            p.UnitPrice.Currency,
            p.DefaultQuantity,
            p.Category,
            p.Sku,
            p.TaxRate,
            p.IsActive)).ToList();

        var result = new PaginatedResponse<ProductDto>
        {
            Items = items,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount
        };

        return HttpResult<PaginatedResponse<ProductDto>>.Ok(result);
    }
}
