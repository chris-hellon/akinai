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

public class GetClientProfitabilityHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService)
    : IHandle<GetClientProfitability, List<ClientProfitDto>>
{
    public async Task<HttpResult<List<ClientProfitDto>>> Handle(
        GetClientProfitability request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<List<ClientProfitDto>>.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

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
                _ => (new DateOnly(today.Year, 1, 1), today)
            };
        }
        if (to < from) (from, to) = (to, from);

        var reporting = !string.IsNullOrWhiteSpace(request.ReportingCurrency)
            ? request.ReportingCurrency!
            : (await db.BusinessProfiles.Where(b => b.UserId == userId)
                .Select(b => b.DefaultCurrency).FirstOrDefaultAsync(cancellationToken)) ?? "USD";

        // Clients billing in the reporting currency, so revenue and expenses combine
        // without an invented FX conversion.
        var clients = await db.Clients
            .Where(c => c.UserId == userId && c.Currency.ToUpper() == reporting.ToUpper())
            .Select(c => new { c.Id, c.Name })
            .ToListAsync(cancellationToken);
        var clientIds = clients.Select(c => c.Id).ToHashSet();

        var invoices = await db.Invoices
            .Include(i => i.CurrencyTotals)
            .Include(i => i.LineItems)
            .Include(i => i.TimeEntries).ThenInclude(te => te.Job).ThenInclude(j => j.Project).ThenInclude(p => p.Client)
            .Include(i => i.Client)
            .Where(i => i.Client.UserId == userId && clientIds.Contains(i.ClientId))
            .ToListAsync(cancellationToken);

        decimal Total(Invoice inv) => InvoiceTotals.InReporting(inv, reporting);

        var revenueByClient = invoices
            .Where(i => i.Status == InvoiceStatus.Paid && i.PaidDate is { } d && d >= from && d <= to)
            .GroupBy(i => i.ClientId)
            .ToDictionary(g => g.Key, g => (Revenue: g.Sum(Total), Count: g.Count()));

        var outstandingByClient = invoices
            .Where(i => i.Status is InvoiceStatus.Sent or InvoiceStatus.Overdue)
            .GroupBy(i => i.ClientId)
            .ToDictionary(g => g.Key, g => g.Sum(Total));

        var expenses = await db.Expenses
            .Where(e => e.CreatedBy == userId && e.ClientId != null
                && e.ExpenseDate >= from && e.ExpenseDate <= to
                && clientIds.Contains(e.ClientId!.Value))
            .ToListAsync(cancellationToken);
        var expenseByClient = expenses
            .Where(e => string.Equals(e.Amount.Currency, reporting, StringComparison.OrdinalIgnoreCase))
            .GroupBy(e => e.ClientId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(e => e.GetTotalAmount()));

        var results = clients
            .Select(c =>
            {
                var rev = revenueByClient.TryGetValue(c.Id, out var r) ? r.Revenue : 0m;
                var cnt = revenueByClient.TryGetValue(c.Id, out var r2) ? r2.Count : 0;
                var exp = expenseByClient.TryGetValue(c.Id, out var e) ? e : 0m;
                var outstanding = outstandingByClient.TryGetValue(c.Id, out var o) ? o : 0m;
                return new ClientProfitDto(c.Id, c.Name, rev, exp, rev - exp, outstanding, cnt);
            })
            .Where(x => x.Revenue != 0 || x.Expenses != 0 || x.Outstanding != 0)
            .OrderByDescending(x => x.Profit)
            .ToList();

        return HttpResult<List<ClientProfitDto>>.Ok(results);
    }
}
