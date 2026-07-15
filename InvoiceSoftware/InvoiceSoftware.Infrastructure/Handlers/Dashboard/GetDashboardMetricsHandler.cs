using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Domain.Enums;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Shared.Api.Dashboard;
using InvoiceSoftware.Shared.Dtos.Dashboard;
using InvoiceSoftware.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Infrastructure.Handlers.Dashboard;

public class GetDashboardMetricsHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService)
    : IHandle<GetDashboardMetrics, DashboardMetricsDto?>
{
    public async Task<HttpResult<DashboardMetricsDto?>> Handle(
        GetDashboardMetrics request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<DashboardMetricsDto?>.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var now = DateOnly.FromDateTime(DateTime.UtcNow);

        // Calendar-based periods so "this month" means the current calendar month
        // (1st → today), not a rolling 30-day window.
        var periodStart = request.FromDate ?? request.Period switch
        {
            "week" => now.AddDays(-6),
            "quarter" => new DateOnly(now.Year, ((now.Month - 1) / 3) * 3 + 1, 1),
            "year" => new DateOnly(now.Year, 1, 1),
            _ => new DateOnly(now.Year, now.Month, 1)
        };
        var periodEnd = request.ToDate ?? now;

        var businessProfile = await db.BusinessProfiles
            .FirstOrDefaultAsync(bp => bp.UserId == userId, cancellationToken);
        var reporting = !string.IsNullOrWhiteSpace(request.ReportingCurrency)
            ? request.ReportingCurrency!
            : businessProfile?.DefaultCurrency ?? "USD";

        var invoices = await db.Invoices
            .Include(i => i.CurrencyTotals)
            .Include(i => i.LineItems)
            .Include(i => i.TimeEntries)
                .ThenInclude(te => te.Job)
                    .ThenInclude(j => j.Project)
                        .ThenInclude(p => p.Client)
            .Include(i => i.Client)
            .Where(i => i.Client.UserId == userId)
            .ToListAsync(cancellationToken);

        decimal Total(Domain.Entities.Invoice invoice) => InvoiceTotals.InReporting(invoice, reporting);

        var paidInPeriod = invoices
            .Where(i => i.Status == InvoiceStatus.Paid && i.PaidDate is { } d && d >= periodStart && d <= periodEnd)
            .ToList();
        var allPaid = invoices.Where(i => i.Status == InvoiceStatus.Paid).ToList();

        // Only issued invoices (Sent/Overdue) are receivable — a Draft hasn't been sent, so
        // it isn't money owed. Overdue is derived from the due date (not just the Status enum)
        // so it's accurate even between runs of the overdue background sweep.
        var receivable = invoices
            .Where(i => i.Status is InvoiceStatus.Sent or InvoiceStatus.Overdue)
            .ToList();
        var overdue = receivable.Where(i => i.DueDate < now).ToList();
        var outstanding = receivable.Where(i => i.DueDate >= now).ToList();

        var paidRevenue = paidInPeriod.Sum(Total);
        var totalRevenue = allPaid.Sum(Total);
        var outstandingAmount = outstanding.Sum(Total);
        var overdueAmount = overdue.Sum(Total);

        var unbilledTimeEntries = await db.TimeEntries
            .Include(te => te.Job)
                .ThenInclude(j => j.Project)
                    .ThenInclude(p => p.Client)
            .Where(te => te.UserId == userId && te.InvoiceId == null)
            .ToListAsync(cancellationToken);
        var unbilledHours = unbilledTimeEntries.Sum(te => te.Hours.Value);
        // Bill at the quarter-hour rate so the "ready to bill" figure matches what these
        // hours will actually invoice at (see TimeEntry.BillableHours).
        decimal unbilledAmount = unbilledTimeEntries.Sum(te => te.BillableHours * te.Job.GetEffectiveHourlyRate());

        var billableTimeEntries = await db.TimeEntries
            .Where(te => te.UserId == userId && te.Date >= periodStart && te.Date <= periodEnd)
            .ToListAsync(cancellationToken);
        var billableHours = billableTimeEntries.Sum(te => te.Hours.Value);

        var activeClients = await db.Clients
            .Where(c => c.UserId == userId)
            .CountAsync(cancellationToken);
        var activeProjects = await db.Projects
            .Include(p => p.Client)
            .Where(p => p.Client.UserId == userId && p.Status == ProjectStatus.Active)
            .CountAsync(cancellationToken);

        var dto = new DashboardMetricsDto(
            TotalRevenue: totalRevenue,
            PaidRevenue: paidRevenue,
            OutstandingAmount: outstandingAmount,
            OverdueAmount: overdueAmount,
            TotalInvoices: invoices.Count,
            PaidInvoices: paidInPeriod.Count,
            OutstandingInvoices: outstanding.Count,
            OverdueInvoices: overdue.Count,
            BillableHours: billableHours,
            UnbilledHours: unbilledHours,
            UnbilledAmount: unbilledAmount,
            ActiveClients: activeClients,
            ActiveProjects: activeProjects);

        return HttpResult<DashboardMetricsDto?>.Ok(dto);
    }
}
