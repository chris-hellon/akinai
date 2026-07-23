using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.RecurringInvoices;
using Akinai.Shared.Dtos.RecurringInvoices;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.RecurringInvoices;

public class GetRecurringInvoiceHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService)
    : IHandle<GetRecurringInvoice, RecurringInvoiceDetailDto?>
{
    public async Task<HttpResult<RecurringInvoiceDetailDto?>> Handle(
        GetRecurringInvoice request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<RecurringInvoiceDetailDto?>.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var recurring = await db.RecurringInvoices
            .Include(r => r.Client)
            .Include(r => r.LineItems.OrderBy(li => li.Order))
                .ThenInclude(li => li.Product)
            .FirstOrDefaultAsync(r => r.Id == request.Id && r.UserId == userId, cancellationToken);

        if (recurring == null)
            return HttpResult<RecurringInvoiceDetailDto?>.Ok(null);

        var lineItems = recurring.LineItems.Select(li => new RecurringInvoiceLineItemDto(
            li.Id,
            li.ProductId,
            li.Product?.Name,
            li.Description,
            li.Quantity,
            li.UnitPrice.Amount,
            li.UnitPrice.Currency,
            li.Order,
            li.LineTotal.Amount)).ToList();

        var subtotal = recurring.LineItems.Sum(li => li.LineTotal.Amount);
        var taxAmount = subtotal * recurring.TaxRate / 100;
        var total = subtotal + taxAmount;

        var dto = new RecurringInvoiceDetailDto(
            recurring.Id,
            recurring.ClientId,
            recurring.Client.Name,
            recurring.TemplateName,
            recurring.Notes,
            recurring.Terms,
            recurring.Footer,
            recurring.TaxRate,
            recurring.Currency,
            recurring.FrequencyInterval,
            recurring.Frequency.ToString(),
            recurring.StartDate,
            recurring.EndDate,
            recurring.DueDays,
            recurring.IsActive,
            recurring.LastGeneratedDate,
            recurring.NextInvoiceDate,
            recurring.GeneratedCount,
            lineItems,
            subtotal,
            taxAmount,
            total,
            recurring.CreatedAt,
            recurring.ModifiedAt);

        return HttpResult<RecurringInvoiceDetailDto?>.Ok(dto);
    }
}
