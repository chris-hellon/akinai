using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.Entities;
using Akinai.Domain.Enums;
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
    IInvoiceTotalService totalService,
    IInvoicePaymentCurrencyService paymentCurrencyService) : IHandle<UpdateInvoice>
{
    public async Task<HttpResult> Handle(UpdateInvoice request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult.Unauthorized();

        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == request.Id && i.Client.UserId == userId, cancellationToken);
        if (invoice is null) return HttpResult.NotFound();

        // A sent invoice is a document someone else is holding. Editing it would change
        // what they owe after they were told, and would leave the evidence pack — the
        // hash of the PDF that actually went out — describing a document that no longer
        // exists.
        //
        // Invoice.UpdateDetails already refuses this, but by throwing from deep inside
        // the handler, which reaches the caller as a 500 rather than an answer. It also
        // only holds because it happens to run first: the time entries, expenses and line
        // items below are mutated through the DbSet rather than the entity, so nothing
        // but that ordering stops them. Stating it here makes the refusal explicit and
        // independent of the order the rest of the method happens to be written in.
        if (invoice.Status != InvoiceStatus.Draft)
            return HttpResult.Forbidden();

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
            // Job → Project → Client loaded because the effective rate cascades through
            // all three, and it is captured on the entry as we link it.
            var toLink = await db.TimeEntries
                .Include(e => e.Job)
                    .ThenInclude(j => j.Project)
                        .ThenInclude(p => p.Client)
                .Where(e => request.AddedTimeEntryIds.Contains(e.Id))
                .ToListAsync(cancellationToken);
            foreach (var entry in toLink)
            {
                entry.LinkToInvoice(request.Id, entry.Job.GetEffectiveHourlyRate());
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

        // After the totals, because each accepted currency is a conversion of the
        // final total — running it first would convert a figure that then changed.
        await paymentCurrencyService.SetAsync(
            db, invoice.Id, userId, request.PaymentCurrencies, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return HttpResult.NoContent();
    }
}
