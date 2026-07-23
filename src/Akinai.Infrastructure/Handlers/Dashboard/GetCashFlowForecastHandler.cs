using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.Enums;
using Akinai.Infrastructure.Data;
using Akinai.Infrastructure.Services;
using Akinai.Shared.Api.Dashboard;
using Akinai.Shared.Dtos.Dashboard;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Dashboard;

public class GetCashFlowForecastHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService,
    IExchangeRateService exchangeRates)
    : IHandle<GetCashFlowForecast, CashFlowForecastDto?>
{
    public async Task<HttpResult<CashFlowForecastDto?>> Handle(
        GetCashFlowForecast request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<CashFlowForecastDto?>.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var months = Math.Clamp(request.Months > 0 ? request.Months : 3, 1, 12);
        var now = DateOnly.FromDateTime(DateTime.UtcNow);

        var businessProfile = await db.BusinessProfiles
            .FirstOrDefaultAsync(bp => bp.UserId == userId, cancellationToken);
        var reporting = !string.IsNullOrWhiteSpace(request.ReportingCurrency)
            ? request.ReportingCurrency!
            : businessProfile?.DefaultCurrency ?? "USD";

        // Outstanding invoices — amounts taken from the currency snapshot in the reporting currency.
        var outstandingInvoices = await db.Invoices
            .Include(i => i.CurrencyTotals)
            .Include(i => i.LineItems)
            .Include(i => i.TimeEntries)
                .ThenInclude(te => te.Job)
                    .ThenInclude(j => j.Project)
                        .ThenInclude(p => p.Client)
            .Include(i => i.Client)
            .Where(i => (i.Status == InvoiceStatus.Sent || i.Status == InvoiceStatus.Overdue) &&
                        i.Client.UserId == userId)
            .ToListAsync(cancellationToken);

        var recurringInvoices = await db.RecurringInvoices
            .Include(r => r.LineItems)
            .Where(r => r.UserId == userId && r.IsActive)
            .ToListAsync(cancellationToken);

        var recurringExpenses = await db.RecurringExpenses
            .Where(e => e.CreatedBy == userId && e.IsActive)
            .ToListAsync(cancellationToken);

        // Pre-convert each recurring item's per-occurrence amount into the reporting currency.
        var riAmounts = new Dictionary<Guid, decimal>();
        foreach (var ri in recurringInvoices)
        {
            var subtotal = ri.LineItems.Sum(li => li.LineTotal.Amount);
            var total = subtotal + (subtotal * ri.TaxRate / 100);
            riAmounts[ri.Id] = (await exchangeRates.ConvertAsync(total, ri.Currency, reporting, cancellationToken)) ?? total;
        }

        var reAmounts = new Dictionary<Guid, decimal>();
        foreach (var re in recurringExpenses)
        {
            reAmounts[re.Id] = (await exchangeRates.ConvertAsync(re.Amount.Amount, re.Amount.Currency, reporting, cancellationToken)) ?? re.Amount.Amount;
        }

        var dataPoints = new List<CashFlowPointDto>();
        var currentMonth = new DateOnly(now.Year, now.Month, 1);
        var cumulativeBalance = 0m;

        for (var i = 0; i < months; i++)
        {
            var monthStart = currentMonth.AddMonths(i);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);

            var expectedIncome = outstandingInvoices
                .Where(inv => inv.DueDate >= monthStart && inv.DueDate <= monthEnd)
                .Sum(inv => InvoiceTotals.InReporting(inv, reporting));

            foreach (var ri in recurringInvoices)
            {
                var nextDate = ri.NextInvoiceDate;
                while (nextDate <= monthEnd)
                {
                    if (nextDate >= monthStart)
                        expectedIncome += riAmounts[ri.Id];
                    nextDate = CalculateNextDate(nextDate, ri.FrequencyInterval, ri.Frequency);
                }
            }

            var expectedExpenses = 0m;
            foreach (var re in recurringExpenses)
            {
                var nextDate = re.NextExpenseDate;
                while (nextDate <= monthEnd)
                {
                    if (nextDate >= monthStart)
                        expectedExpenses += reAmounts[re.Id];
                    nextDate = CalculateNextDate(nextDate, re.FrequencyInterval, re.Frequency);
                }
            }

            cumulativeBalance += expectedIncome - expectedExpenses;
            dataPoints.Add(new CashFlowPointDto(monthStart, expectedIncome, expectedExpenses, cumulativeBalance));
        }

        return HttpResult<CashFlowForecastDto?>.Ok(new CashFlowForecastDto(dataPoints, reporting));
    }

    private static DateOnly CalculateNextDate(DateOnly current, int interval, RecurrenceFrequency frequency)
    {
        return frequency switch
        {
            RecurrenceFrequency.Day => current.AddDays(interval),
            RecurrenceFrequency.Week => current.AddDays(interval * 7),
            RecurrenceFrequency.Month => current.AddMonths(interval),
            RecurrenceFrequency.Year => current.AddYears(interval),
            _ => current.AddMonths(interval)
        };
    }
}
