using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.Entities;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Estimates;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Estimates;

public class ConvertEstimateToInvoiceHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService)
    : IHandle<ConvertEstimateToInvoice, Guid>
{
    public async Task<HttpResult<Guid>> Handle(ConvertEstimateToInvoice request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<Guid>.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var estimate = await db.Estimates
            .Include(e => e.LineItems)
            .Include(e => e.Client)
            .FirstOrDefaultAsync(e => e.Id == request.Id && e.UserId == userId, cancellationToken);

        if (estimate == null)
            return HttpResult<Guid>.NotFound();

        if (estimate.Status != Domain.Enums.EstimateStatus.Accepted)
            return HttpResult<Guid>.NotFound();

        // Generate invoice number
        var lastInvoice = await db.Invoices.OrderByDescending(i => i.InvoiceNumber).FirstOrDefaultAsync(cancellationToken);
        var nextNumber = 1;
        if (lastInvoice != null && lastInvoice.InvoiceNumber.StartsWith("INV-"))
        {
            if (int.TryParse(lastInvoice.InvoiceNumber[4..], out var num))
                nextNumber = num + 1;
        }
        var invoiceNumber = $"INV-{nextNumber:D5}";

        // Build notes including estimate reference and line items
        var lineItemsSummary = string.Join("\n", estimate.LineItems.OrderBy(li => li.Order)
            .Select(li => $"- {li.Description}: {li.Quantity} x {li.UnitPrice.Amount:F2} = {li.LineTotal.Amount:F2}"));

        var notes = $"Converted from {estimate.EstimateNumber}\n\nLine Items:\n{lineItemsSummary}";
        if (!string.IsNullOrEmpty(estimate.Notes))
            notes += $"\n\nOriginal Notes:\n{estimate.Notes}";

        // Create invoice from estimate
        var issueDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var dueDate = issueDate.AddDays(30); // Default 30 days payment term

        var invoice = Invoice.Create(
            invoiceNumber,
            estimate.ClientId,
            issueDate,
            dueDate,
            estimate.TaxRate,
            notes,
            estimate.Currency);

        db.Invoices.Add(invoice);

        // Mark estimate as converted
        estimate.MarkAsConverted(invoice.Id);

        await db.SaveChangesAsync(cancellationToken);
        return HttpResult<Guid>.Created(invoice.Id);
    }
}
