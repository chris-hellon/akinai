using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.Entities;
using Akinai.Infrastructure.Data;
using Akinai.Infrastructure.Services;
using Akinai.Shared.Api.Invoices;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Invoices;

public class UpdateInvoiceHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService,
    IEntitlementService entitlementService,
    IInvoiceTotalService totalService) : IHandle<UpdateInvoice>
{
    public async Task<HttpResult> Handle(UpdateInvoice request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult.Unauthorized();

        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == request.Id && i.Client.UserId == userId, cancellationToken);
        if (invoice is null) return HttpResult.NotFound();

        var entitlements = await entitlementService.GetAsync(userId, cancellationToken);
        if (!InvoiceLanguageRequest.TryResolve(request.Language, entitlements, out var language))
            return HttpResult.Forbidden();

        // The template chosen in the editor. Null is a deliberate choice — "use
        // my default" — not an accident, so it is honoured rather than falling
        // back to whatever the invoice previously had.
        invoice.UpdateDetails(
            request.IssueDate, request.DueDate, request.TaxRate, request.Notes,
            request.TemplateId, language);

        // Remove time entries
        if (request.RemovedTimeEntryIds?.Count > 0)
        {
            var toUnlink = await db.TimeEntries
                .Where(e => request.RemovedTimeEntryIds.Contains(e.Id))
                .ToListAsync(cancellationToken);
            foreach (var entry in toUnlink)
            {
                entry.UnlinkFromInvoice();
            }
        }

        // Add time entries
        if (request.AddedTimeEntryIds?.Count > 0)
        {
            var toLink = await db.TimeEntries
                .Where(e => request.AddedTimeEntryIds.Contains(e.Id))
                .ToListAsync(cancellationToken);
            foreach (var entry in toLink)
            {
                entry.LinkToInvoice(request.Id);
            }
        }

        // Remove expenses
        if (request.RemovedExpenseIds?.Count > 0)
        {
            var toUnlink = await db.Expenses
                .Where(e => request.RemovedExpenseIds.Contains(e.Id))
                .ToListAsync(cancellationToken);
            foreach (var expense in toUnlink)
            {
                expense.UnmarkBilled();
            }
        }

        // Add expenses
        if (request.AddedExpenseIds?.Count > 0)
        {
            var toLink = await db.Expenses
                .Where(e => request.AddedExpenseIds.Contains(e.Id))
                .ToListAsync(cancellationToken);
            foreach (var expense in toLink)
            {
                expense.MarkAsBilled(request.Id);
            }
        }

        // Update product line items - replace all
        if (request.ProductLineItems != null)
        {
            // Remove existing product line items
            var existingLineItems = await db.InvoiceLineItems
                .Where(li => li.InvoiceId == request.Id)
                .ToListAsync(cancellationToken);
            db.InvoiceLineItems.RemoveRange(existingLineItems);

            // Add new product line items
            var order = 0;
            foreach (var item in request.ProductLineItems)
            {
                var lineItem = InvoiceLineItem.Create(
                    request.Id,
                    item.Description,
                    item.Quantity,
                    item.UnitPrice,
                    invoice.Currency,
                    order++,
                    item.ProductId);
                db.InvoiceLineItems.Add(lineItem);
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        // Re-snapshot totals after the invoice's lines/time/expenses changed.
        // The invoice was fetched scoped to this user, so its owner is `userId` —
        // no need to look it up via the client again.
        await totalService.RecalculateAsync(db, invoice.Id, userId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return HttpResult.NoContent();
    }
}
