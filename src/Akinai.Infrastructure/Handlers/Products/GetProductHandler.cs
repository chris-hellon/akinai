using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Products;
using Akinai.Shared.Dtos.Products;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Products;

public class GetProductHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService)
    : IHandle<GetProduct, ProductDetailDto?>
{
    public async Task<HttpResult<ProductDetailDto?>> Handle(GetProduct request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<ProductDetailDto?>.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var product = await db.Products
            .FirstOrDefaultAsync(p => p.Id == request.Id && p.UserId == userId, cancellationToken);

        if (product == null)
            return HttpResult<ProductDetailDto?>.Ok(null);

        var dto = new ProductDetailDto(
            product.Id,
            product.Name,
            product.Description,
            product.UnitPrice.Amount,
            product.UnitPrice.Currency,
            product.DefaultQuantity,
            product.Category,
            product.Sku,
            product.TaxRate,
            product.IsActive,
            product.CreatedAt,
            product.ModifiedAt);

        return HttpResult<ProductDetailDto?>.Ok(dto);
    }
}
