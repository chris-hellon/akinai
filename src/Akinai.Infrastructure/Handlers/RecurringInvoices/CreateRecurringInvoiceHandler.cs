using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.Entities;
using Akinai.Domain.Enums;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.RecurringInvoices;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.RecurringInvoices;

public class CreateRecurringInvoiceHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService, IEntitlementService entitlementService)
    : IHandle<CreateRecurringInvoice, Guid>
{
    public async Task<HttpResult<Guid>> Handle(CreateRecurringInvoice request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<Guid>.Unauthorized();

        // Recurring billing is a paid feature. Existing schedules keep running and stay
        // visible; only creating new ones is gated.
        var entitlements = await entitlementService.GetAsync(userId, cancellationToken);
        if (!entitlements.RecurringBilling) return HttpResult<Guid>.Forbidden();

        if (!Enum.TryParse<RecurrenceFrequency>(request.Frequency, out var frequency))
            return HttpResult<Guid>.NotFound();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var recurring = RecurringInvoice.Create(
            userId,
            request.ClientId,
            request.TemplateName,
            request.Currency,
            request.FrequencyInterval,
            frequency,
            request.StartDate,
            request.DueDays,
            request.EndDate,
            request.TaxRate,
            request.Notes,
            request.Terms,
            request.Footer);

        foreach (var item in request.LineItems)
        {
            recurring.AddLineItem(
                item.Description,
                item.Quantity,
                item.UnitPrice,
                item.ProductId);
        }

        db.RecurringInvoices.Add(recurring);
        await db.SaveChangesAsync(cancellationToken);
        return HttpResult<Guid>.Created(recurring.Id);
    }
}
