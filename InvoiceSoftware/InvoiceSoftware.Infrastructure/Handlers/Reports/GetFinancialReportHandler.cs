using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Domain.Entities;
using InvoiceSoftware.Domain.Enums;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Infrastructure.Handlers.Dashboard;
using InvoiceSoftware.Shared.Api.Reports;
using InvoiceSoftware.Shared.Dtos.Reports;
using InvoiceSoftware.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Infrastructure.Handlers.Reports;

public class GetFinancialReportHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService)
    : IHandle<GetFinancialReport, FinancialReportDto?>
{
    public async Task<HttpResult<FinancialReportDto?>> Handle(
        GetFinancialReport request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<FinancialReportDto?>.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // ----- Resolve the reporting window -----
        DateOnly from, to;
        if (request.FromDate is { } f && request.ToDate is { } t)
        {
            from = f;
            to = t;
        }
        else
        {
            (from, to) = request.Period switch
            {
                "month" => (new DateOnly(today.Year, today.Month, 1), today),
                "quarter" => (new DateOnly(today.Year, ((today.Month - 1) / 3) * 3 + 1, 1), today),
                "last-year" => (new DateOnly(today.Year - 1, 1, 1), new DateOnly(today.Year - 1, 12, 31)),
                _ => (new DateOnly(today.Year, 1, 1), today) // "year"
            };
        }
        if (to < from) (from, to) = (to, from);

        // ----- Reporting currency -----
        var reporting = !string.IsNullOrWhiteSpace(request.ReportingCurrency)
            ? request.ReportingCurrency!
            : (await db.BusinessProfiles.Where(b => b.UserId == userId)
                .Select(b => b.DefaultCurrency).FirstOrDefaultAsync(cancellationToken)) ?? "USD";

        // ----- Load data -----
        var invoices = await db.Invoices
            .Include(i => i.CurrencyTotals)
            .Include(i => i.LineItems)
            .Include(i => i.TimeEntries).ThenInclude(te => te.Job).ThenInclude(j => j.Project).ThenInclude(p => p.Client)
            .Include(i => i.Client)
            .Where(i => i.Client.UserId == userId)
            .ToListAsync(cancellationToken);

        var expenses = await db.Expenses
            .Where(e => e.CreatedBy == userId)
            .ToListAsync(cancellationToken);

        decimal Total(Invoice inv) => InvoiceTotals.InReporting(inv, reporting);

        // ----- Income (revenue collected in the window) -----
        var paidInPeriod = invoices
            .Where(i => i.Status == InvoiceStatus.Paid && i.PaidDate is { } d && d >= from && d <= to)
            .ToList();
        var income = paidInPeriod.Sum(Total);

        // Tax portion of the reporting total: Total = Subtotal * (1 + r/100) ⇒ tax = Total * r/(100+r).
        var taxCollected = paidInPeriod.Sum(i =>
            i.TaxRate > 0 ? Math.Round(Total(i) * i.TaxRate / (100m + i.TaxRate), 2) : 0m);

        // ----- Expenses (in the reporting currency only — no cross-currency conversion) -----
        var expensesInPeriod = expenses
            .Where(e => e.ExpenseDate >= from && e.ExpenseDate <= to
                && string.Equals(e.Amount.Currency, reporting, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var expenseTotal = expensesInPeriod.Sum(e => e.GetTotalAmount());
        var taxPaid = expensesInPeriod.Sum(e => e.TaxAmount);

        var expensesByCategory = expensesInPeriod
            .GroupBy(e => e.Category)
            .Select(g => new ReportCategoryDto(g.Key.ToString(), g.Sum(e => e.GetTotalAmount())))
            .Where(c => c.Amount != 0)
            .OrderByDescending(c => c.Amount)
            .ToList();

        // ----- Monthly income vs expenses series -----
        var monthly = new List<ReportMonthlyDto>();
        var cursor = new DateOnly(from.Year, from.Month, 1);
        var lastMonth = new DateOnly(to.Year, to.Month, 1);
        var guard = 0;
        while (cursor <= lastMonth && guard++ < 120)
        {
            var mStart = cursor;
            var mEnd = cursor.AddMonths(1).AddDays(-1);
            var mIncome = invoices
                .Where(i => i.Status == InvoiceStatus.Paid && i.PaidDate is { } d && d >= mStart && d <= mEnd)
                .Sum(Total);
            var mExpense = expensesInPeriod
                .Where(e => e.ExpenseDate >= mStart && e.ExpenseDate <= mEnd)
                .Sum(e => e.GetTotalAmount());
            monthly.Add(new ReportMonthlyDto(cursor.ToString("MMM yyyy"), mIncome, mExpense, mIncome - mExpense));
            cursor = cursor.AddMonths(1);
        }

        // ----- Accounts receivable aging (current snapshot, not window-bound) -----
        var receivable = invoices
            .Where(i => i.Status is InvoiceStatus.Sent or InvoiceStatus.Overdue)
            .ToList();
        var outstandingTotal = receivable.Where(i => i.DueDate >= today).Sum(Total);
        var overdueTotal = receivable.Where(i => i.DueDate < today).Sum(Total);

        ReportAgingBucketDto Bucket(string label, Func<int, bool> daysPredicate)
        {
            var items = receivable.Where(i => daysPredicate(today.DayNumber - i.DueDate.DayNumber)).ToList();
            return new ReportAgingBucketDto(label, items.Count, items.Sum(Total));
        }
        var aging = new List<ReportAgingBucketDto>
        {
            Bucket("Current", days => days <= 0),
            Bucket("1–30 days", days => days is >= 1 and <= 30),
            Bucket("31–60 days", days => days is >= 31 and <= 60),
            Bucket("61–90 days", days => days is >= 61 and <= 90),
            Bucket("90+ days", days => days > 90)
        };

        // ----- Top clients by revenue collected in the window -----
        var topClients = paidInPeriod
            .GroupBy(i => new { i.ClientId, i.Client.Name })
            .Select(g => new ReportTopClientDto(g.Key.ClientId, g.Key.Name, g.Sum(Total), g.Count()))
            .OrderByDescending(c => c.Amount)
            .Take(6)
            .ToList();

        var dto = new FinancialReportDto(
            Currency: reporting,
            FromDate: from,
            ToDate: to,
            Income: income,
            ExpenseTotal: expenseTotal,
            NetProfit: income - expenseTotal,
            TaxCollected: taxCollected,
            TaxPaid: taxPaid,
            OutstandingTotal: outstandingTotal,
            OverdueTotal: overdueTotal,
            PaidInvoiceCount: paidInPeriod.Count,
            ExpenseCount: expensesInPeriod.Count,
            ExpensesByCategory: expensesByCategory,
            Monthly: monthly,
            Aging: aging,
            TopClients: topClients);

        return HttpResult<FinancialReportDto?>.Ok(dto);
    }
}
