using Akinai.Domain.Entities;
using Akinai.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Services;

public class InvoiceTotalService(IExchangeRateService exchangeRates) : IInvoiceTotalService
{
    public async Task RecalculateAsync(ApplicationDbContext db, Guid invoiceId, string userId, CancellationToken cancellationToken = default)
    {
        var invoice = await db.Invoices
            .Include(i => i.TimeEntries)
                .ThenInclude(te => te.Job)
                    .ThenInclude(j => j.Project)
                        .ThenInclude(p => p.Client)
            .Include(i => i.LineItems)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);

        if (invoice is null)
            return;

        // Subtotal = billed time (at the current effective rate) + product line items + billable expenses.
        decimal subtotal = 0;
        foreach (var te in invoice.TimeEntries)
            // Bill to the nearest quarter-hour, matching every customer-facing total
            // (invoice view, PDF, email). Raw hours here made the stored total drift.
            subtotal += te.BillableHours * te.BillingRate;
        foreach (var li in invoice.LineItems)
            subtotal += li.LineTotal.Amount;

        var billedExpenses = await db.Expenses
            .Where(e => e.InvoiceId == invoiceId)
            .ToListAsync(cancellationToken);
        foreach (var expense in billedExpenses)
            subtotal += expense.GetTotalAmount();

        var taxAmount = Math.Round(subtotal * invoice.TaxRate / 100m, 2);
        var total = subtotal + taxAmount;

        // Target currencies: this invoice's own currency + every currency the owner's clients use
        // + the business's default/reporting currency.
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { invoice.Currency };

        var clientCurrencies = await db.Clients
            .Where(c => c.UserId == userId)
            .Select(c => c.Currency)
            .Distinct()
            .ToListAsync(cancellationToken);
        foreach (var c in clientCurrencies)
            if (!string.IsNullOrWhiteSpace(c)) targets.Add(c);

        var defaultCurrency = await db.BusinessProfiles
            .Where(b => b.UserId == userId)
            .Select(b => b.DefaultCurrency)
            .FirstOrDefaultAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(defaultCurrency))
            targets.Add(defaultCurrency);

        var currencyTotals = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            [invoice.Currency] = total
        };
        foreach (var currency in targets)
        {
            if (currencyTotals.ContainsKey(currency)) continue;
            var converted = await exchangeRates.ConvertAsync(total, invoice.Currency, currency, cancellationToken);
            if (converted.HasValue)
                currencyTotals[currency] = converted.Value;
        }

        invoice.SetTotals(subtotal, taxAmount, total);

        // Replace the per-currency snapshot rows via the DbSet so EF tracks them as
        // deletes + inserts. (Adding new rows through the navigation collection makes EF
        // mis-read their pre-assigned Guid keys as existing rows and emit UPDATEs.)
        var existingTotals = await db.InvoiceCurrencyTotals
            .Where(ct => ct.InvoiceId == invoiceId)
            .ToListAsync(cancellationToken);
        if (existingTotals.Count > 0)
        {
            db.InvoiceCurrencyTotals.RemoveRange(existingTotals);
            await db.SaveChangesAsync(cancellationToken);
        }

        foreach (var (currency, amount) in currencyTotals)
            db.InvoiceCurrencyTotals.Add(InvoiceCurrencyTotal.Create(invoiceId, currency, amount));
    }
}
