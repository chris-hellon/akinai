using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Estimates;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Estimates;

public class UpdateEstimateHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService,
    IEntitlementService entitlementService)
    : IHandle<UpdateEstimate>
{
    public async Task<HttpResult> Handle(UpdateEstimate request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var estimate = await db.Estimates
            .Include(e => e.LineItems)
            .FirstOrDefaultAsync(e => e.Id == request.Id && e.UserId == userId, cancellationToken);

        if (estimate == null)
            return HttpResult.NotFound();

        var entitlements = await entitlementService.GetAsync(userId, cancellationToken);
        if (!Invoices.InvoiceLanguageRequest.TryResolve(request.Language, entitlements, out var language))
            return HttpResult.Forbidden();

        estimate.UpdateDetails(
            request.EstimateDate,
            request.ValidDays,
            request.TaxRate,
            request.Notes,
            request.Terms,
            language);

        // Update template
        estimate.SetTemplate(request.TemplateId);

        // Clear and re-add line items
        estimate.ClearLineItems();

        foreach (var item in request.LineItems)
        {
            estimate.AddLineItem(
                item.Description,
                item.Quantity,
                item.UnitPrice,
                item.ProductId);
        }

        await db.SaveChangesAsync(cancellationToken);
        return HttpResult.NoContent();
    }
}
