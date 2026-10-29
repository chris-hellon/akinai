using Akinai.Domain.Entities;
using Akinai.Domain.Enums;
using Akinai.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Services;

public class InvoicePaymentCurrencyService(IExchangeRateService exchangeRates) : IInvoicePaymentCurrencyService
{
    public async Task SetAsync(
        ApplicationDbContext db,
        Guid invoiceId,
        string userId,
        IReadOnlyList<string>? currencies,
        CancellationToken cancellationToken = default)
    {
        if (currencies is null) return;

        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);
        if (invoice is null) return;

        // Only currencies the owner has somewhere to receive. The form offers exactly
        // these, so a code that is not among them arrived from a stale page or by hand,
        // and honouring it would print an account number the invoice does not have.
        var payable = await db.CurrencyPaymentSettings
            .Where(c => c.UserId == userId)
            .Select(c => new
            {
                c.CurrencyCode,
                Reachable = c.BankAccountNumber != null || c.BankIban != null
                            || c.UpiVpa != null || c.PixKey != null || c.PromptPayId != null,
            })
            .ToListAsync(cancellationToken);

        var allowed = payable
            .Where(p => p.Reachable && !string.IsNullOrWhiteSpace(p.CurrencyCode))
            .Select(p => p.CurrencyCode!.Trim().ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var wanted = currencies
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToUpperInvariant())
            // The invoice's own currency is always accepted and is rendered as the
            // primary block. Storing it too would print the same account twice.
            .Where(c => !string.Equals(c, invoice.Currency, StringComparison.OrdinalIgnoreCase))
            .Where(allowed.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Replaced through the DbSet rather than the navigation collection, for the same
        // reason InvoiceTotalService does: EF reads a pre-assigned Guid key on a new row
        // added through a navigation as an existing row, and emits an UPDATE that hits
        // nothing.
        var existing = await db.InvoicePaymentCurrencies
            .Where(pc => pc.InvoiceId == invoiceId)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
        {
            db.InvoicePaymentCurrencies.RemoveRange(existing);
            await db.SaveChangesAsync(cancellationToken);
        }

        // Once the invoice has left Draft the client has a copy of it, and the amount
        // they were told they could pay is the amount they owe. Editing a sent invoice
        // must not restate that at today's rate — the same rule that freezes an entry's
        // billing rate the moment it is invoiced.
        //
        // A draft is still being written, and its total can move, so its conversions are
        // redone on each save. A currency added for the first time is always converted
        // now, sent or not: there is no earlier rate for an offer that did not exist.
        var isIssued = invoice.Status != InvoiceStatus.Draft;
        var alreadyAgreed = existing.ToDictionary(
            pc => pc.Currency, pc => pc.Amount, StringComparer.OrdinalIgnoreCase);

        foreach (var currency in wanted)
        {
            decimal amount;
            if (isIssued && alreadyAgreed.TryGetValue(currency, out var agreed))
            {
                amount = agreed;
            }
            else
            {
                var converted = await exchangeRates.ConvertAsync(
                    invoice.Total, invoice.Currency, currency, cancellationToken);

                // No rate, no offer. An accepted currency with a missing or guessed
                // amount is worse than not offering it — the client would pay a number
                // nobody agreed to.
                if (!converted.HasValue) continue;
                amount = converted.Value;
            }

            db.InvoicePaymentCurrencies.Add(
                InvoicePaymentCurrency.Create(invoiceId, currency, amount));
        }
    }
}
