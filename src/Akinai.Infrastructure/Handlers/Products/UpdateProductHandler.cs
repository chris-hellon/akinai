using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Products;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Products;

public class UpdateProductHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService)
    : IHandle<UpdateProduct>
{
    public async Task<HttpResult> Handle(UpdateProduct request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var product = await db.Products
            .FirstOrDefaultAsync(p => p.Id == request.Id && p.UserId == userId, cancellationToken);

        if (product == null)
            return HttpResult.NotFound();

        product.Update(
            request.Name,
            request.UnitPrice,
            request.Currency,
            request.Description,
            request.DefaultQuantity,
            request.Category,
            request.Sku,
            request.TaxRate);

        if (request.IsActive)
            product.Activate();
        else
            product.Deactivate();

        await db.SaveChangesAsync(cancellationToken);
        return HttpResult.NoContent();
    }
}
