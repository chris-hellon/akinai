using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.Enums;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Dashboard;
using Akinai.Shared.Dtos.Dashboard;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Dashboard;

public class GetRevenueChartHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService)
    : IHandle<GetRevenueChart, RevenueChartDataDto>
{
    public async Task<HttpResult<RevenueChartDataDto>> Handle(
        GetRevenueChart request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<RevenueChartDataDto>.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var months = request.Months > 0 ? request.Months : 12;
        var dataPoints = new List<RevenueDataPoint>();

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

        var expenses = await db.Expenses
            .Where(e => e.CreatedBy == userId && e.InvoiceId == null)
            .ToListAsync(cancellationToken);

        var businessProfile = await db.BusinessProfiles
            .FirstOrDefaultAsync(bp => bp.UserId == userId, cancellationToken);
        var reporting = !string.IsNullOrWhiteSpace(request.ReportingCurrency)
            ? request.ReportingCurrency!
            : businessProfile?.DefaultCurrency ?? "USD";

        for (int i = months - 1; i >= 0; i--)
        {
            var monthStart = new DateTime(now.Year, now.Month, 1).AddMonths(-i);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            var label = monthStart.ToString("MMM yyyy");
            var monthStartDate = DateOnly.FromDateTime(monthStart);
            var monthEndDate = DateOnly.FromDateTime(monthEnd);

            var monthInvoices = invoices
                .Where(inv => inv.Status == InvoiceStatus.Paid &&
                              inv.PaidDate is { } d && d >= monthStartDate && d <= monthEndDate);

            var revenue = monthInvoices.Sum(inv => InvoiceTotals.InReporting(inv, reporting));

            var monthExpenses = expenses
                .Where(e => e.ExpenseDate >= monthStartDate && e.ExpenseDate <= monthEndDate)
                .Sum(e => e.Amount.Amount);

            dataPoints.Add(new RevenueDataPoint(label, revenue, monthExpenses, revenue - monthExpenses));
        }

        return HttpResult<RevenueChartDataDto>.Ok(new RevenueChartDataDto(dataPoints, reporting, request.Period));
    }
}
