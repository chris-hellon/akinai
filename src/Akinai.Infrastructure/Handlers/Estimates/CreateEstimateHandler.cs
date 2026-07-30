using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.Entities;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Estimates;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Estimates;

public class CreateEstimateHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService, IEntitlementService entitlementService)
    : IHandle<CreateEstimate, Guid>
{
    public async Task<HttpResult<Guid>> Handle(CreateEstimate request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<Guid>.Unauthorized();

        // Estimates are a paid feature. Ones already sent stay viewable and signable
        // by the client — gating them after the fact would break a live quote.
        var entitlements = await entitlementService.GetAsync(userId, cancellationToken);
        if (!entitlements.Estimates) return HttpResult<Guid>.Forbidden();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // Generate estimate number
        var lastEstimate = await db.Estimates
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.EstimateNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var estimateNumber = GenerateEstimateNumber(lastEstimate?.EstimateNumber);

        // Same gate as invoices: the picker hides languages the plan lacks,
        // but a crafted request would otherwise buy a paid language for free
        // and spend against the shared translation allowance.
        if (!Invoices.InvoiceLanguageRequest.TryResolve(request.Language, entitlements, out var language))
            return HttpResult<Guid>.Forbidden();

        var estimate = Estimate.Create(
            userId,
            estimateNumber,
            request.ClientId,
            request.EstimateDate,
            request.Currency,
            request.ValidDays,
            request.TaxRate,
            request.Notes,
            request.Terms,
            language);

        // Set template if provided
        if (request.TemplateId.HasValue)
        {
            estimate.SetTemplate(request.TemplateId.Value);
        }

        // Add line items
        foreach (var item in request.LineItems)
        {
            estimate.AddLineItem(
                item.Description,
                item.Quantity,
                item.UnitPrice,
                item.ProductId);
        }

        db.Estimates.Add(estimate);
        await db.SaveChangesAsync(cancellationToken);
        return HttpResult<Guid>.Created(estimate.Id);
    }

    private static string GenerateEstimateNumber(string? lastNumber)
    {
        if (string.IsNullOrEmpty(lastNumber))
            return "EST-00001";

        var parts = lastNumber.Split('-');
        if (parts.Length == 2 && int.TryParse(parts[1], out var number))
            return $"EST-{(number + 1):D5}";

        return $"EST-00001";
    }
}
