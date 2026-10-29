using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.Entities;
using Akinai.Infrastructure.Data;
using Akinai.Infrastructure.Services;
using Akinai.Shared.Api.Invoices;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Invoices;

public class CreateInvoiceHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService,
    IEntitlementService entitlementService,
    IInvoiceTotalService totalService,
    IInvoicePaymentCurrencyService paymentCurrencyService) : IHandle<CreateInvoice, Guid>
{
    public async Task<HttpResult<Guid>> Handle(CreateInvoice request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult<Guid>.Unauthorized();

        // Plan limits are enforced here, not in the UI. Only creation is
        // limited — existing invoices stay fully readable and exportable on
        // every plan, because they are financial records the account owns.
        var entitlements = await entitlementService.GetAsync(userId, cancellationToken);
        if (!entitlements.CanCreateInvoice) return HttpResult<Guid>.Forbidden();

        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == request.ClientId && c.UserId == userId, cancellationToken);
        var currency = client != null ? client.Currency : "USD";

        var lastInvoice = await db.Invoices.Where(i => i.Client.UserId == userId).OrderByDescending(i => i.InvoiceNumber).FirstOrDefaultAsync(cancellationToken);
        var nextNumber = 1;
        if (lastInvoice != null && lastInvoice.InvoiceNumber.StartsWith("INV-"))
        {
            if (int.TryParse(lastInvoice.InvoiceNumber[4..], out var num))
                nextNumber = num + 1;
        }
        var invoiceNumber = $"INV-{nextNumber:D5}";

        if (!InvoiceLanguageRequest.TryResolve(request.Language, entitlements, out var language))
            return HttpResult<Guid>.Forbidden();

        var invoice = Invoice.Create(
            invoiceNumber,
            request.ClientId,
            request.IssueDate,
            request.DueDate,
            request.TaxRate,
            request.Notes,
            currency,
            language);

        // Set template if provided
        if (request.TemplateId.HasValue)
        {
            invoice.SetTemplate(request.TemplateId.Value);
        }

        db.Invoices.Add(invoice);

        // Link time entries
        if (request.TimeEntryIds.Count > 0)
        {
            // Job → Project → Client loaded because the effective rate cascades through
            // all three, and it is captured on the entry as we link it.
            var entries = await db.TimeEntries
                .Include(e => e.Job)
                    .ThenInclude(j => j.Project)
                        .ThenInclude(p => p.Client)
                .Where(e => request.TimeEntryIds.Contains(e.Id))
                .ToListAsync(cancellationToken);

            foreach (var entry in entries)
            {
                entry.LinkToInvoice(invoice.Id, entry.Job.GetEffectiveHourlyRate());
            }
        }

        // Link expenses
        if (request.ExpenseIds.Count > 0)
        {
            var expenses = await db.Expenses
                .Where(e => request.ExpenseIds.Contains(e.Id))
                .ToListAsync(cancellationToken);

            foreach (var expense in expenses)
            {
                expense.MarkAsBilled(invoice.Id);
            }
        }

        // Create product line items
        if (request.ProductLineItems.Count > 0)
        {
            var order = 0;
            foreach (var item in request.ProductLineItems)
            {
                var lineItem = InvoiceLineItem.Create(
                    invoice.Id,
                    item.Description,
                    item.Quantity,
                    item.UnitPrice,
                    currency,
                    order++,
                    item.ProductId);

                db.InvoiceLineItems.Add(lineItem);
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        // Snapshot totals (own currency + conversions) now that time entries, expenses
        // and line items are persisted and linked.
        if (client is not null)
        {
            await totalService.RecalculateAsync(db, invoice.Id, client.UserId, cancellationToken);

            // After the totals, because each accepted currency is a conversion of the
            // final total — running it first would convert a figure that then changed.
            await paymentCurrencyService.SetAsync(
                db, invoice.Id, client.UserId, request.PaymentCurrencies, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        return HttpResult<Guid>.Created(invoice.Id);
    }
}
