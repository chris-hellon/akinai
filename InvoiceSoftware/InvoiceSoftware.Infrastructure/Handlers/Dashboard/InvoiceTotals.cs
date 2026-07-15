using InvoiceSoftware.Domain.Entities;

namespace InvoiceSoftware.Infrastructure.Handlers.Dashboard;

/// <summary>
/// Resolves an invoice's total in a chosen reporting currency for read/reporting paths.
/// Prefers the per-currency snapshot; falls back to the own-currency snapshot; and finally
/// live-computes from time entries + line items for legacy invoices that predate snapshots.
/// Requires the invoice to be loaded with CurrencyTotals, LineItems, and TimeEntries.Job.
/// </summary>
public static class InvoiceTotals
{
    public static decimal InReporting(Invoice invoice, string reportingCurrency)
    {
        var snapshot = invoice.CurrencyTotals
            .FirstOrDefault(x => string.Equals(x.Currency, reportingCurrency, StringComparison.OrdinalIgnoreCase));
        if (snapshot is not null)
            return snapshot.Amount;

        if (invoice.Total > 0)
            return invoice.Total;

        // Legacy fallback (also includes line items, which the old handlers ignored).
        decimal subtotal = 0;
        foreach (var te in invoice.TimeEntries)
            subtotal += te.BillableHours * te.Job.GetEffectiveHourlyRate();
        foreach (var li in invoice.LineItems)
            subtotal += li.LineTotal.Amount;
        return subtotal + (subtotal * invoice.TaxRate / 100m);
    }
}
