using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.Entities;
using Akinai.Domain.Enums;
using Akinai.Infrastructure.Data;
using Akinai.Infrastructure.Handlers.Dashboard;
using Akinai.Shared.Api.CashFlow;
using Akinai.Shared.Dtos.CashFlow;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.CashFlow;

public class GetCashFlowProjectionHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService)
    : IHandle<GetCashFlowProjection, CashFlowProjectionDto?>
{
    private sealed record ClientStat(double AvgDaysLate, double OnTimeRate, int Sample, string Score);

    public async Task<HttpResult<CashFlowProjectionDto?>> Handle(
        GetCashFlowProjection request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<CashFlowProjectionDto?>.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var horizon = Math.Clamp(request.Days, 14, 365);

        var profile = await db.BusinessProfiles.FirstOrDefaultAsync(bp => bp.UserId == userId, cancellationToken);
        var reporting = !string.IsNullOrWhiteSpace(request.ReportingCurrency)
            ? request.ReportingCurrency!
            : profile?.DefaultCurrency ?? "USD";

        var invoices = await db.Invoices
            .Include(i => i.CurrencyTotals)
            .Include(i => i.LineItems)
            .Include(i => i.TimeEntries).ThenInclude(te => te.Job).ThenInclude(j => j.Project).ThenInclude(p => p.Client)
            .Include(i => i.Client)
            .Where(i => i.Client.UserId == userId)
            .ToListAsync(cancellationToken);

        var bankTx = await db.BankTransactions
            .Where(bt => bt.UserId == userId)
            .Select(bt => new { bt.TransactionDate, bt.Amount, bt.Currency, bt.Description })
            .ToListAsync(cancellationToken);

        decimal Total(Invoice inv) => InvoiceTotals.InReporting(inv, reporting);

        // ---- Per-client payment behaviour, learned from paid history ----
        var stats = invoices
            .Where(i => i.Status == InvoiceStatus.Paid && i.PaidDate is not null)
            .GroupBy(i => i.ClientId)
            .ToDictionary(g => g.Key, g =>
            {
                var late = g.Select(i => i.PaidDate!.Value.DayNumber - i.DueDate.DayNumber).ToList();
                var avg = late.Average();
                var onTime = late.Count(d => d <= 3) / (double)late.Count;
                return new ClientStat(avg, onTime, late.Count, Score(late.Count, avg));
            });

        var clientNames = invoices
            .GroupBy(i => i.ClientId)
            .ToDictionary(g => g.Key, g => g.First().Client.Name);

        // ---- Current balance (anchor rolled forward by later bank movements) ----
        var hasBalance = profile?.CashBalance is not null;
        var currentBalance = profile?.CashBalance ?? 0m;
        if (hasBalance && profile!.CashBalanceAsOf is { } asOf)
        {
            currentBalance += bankTx
                .Where(t => t.TransactionDate > asOf && t.TransactionDate <= today
                    && string.Equals(t.Currency, reporting, StringComparison.OrdinalIgnoreCase))
                .Sum(t => t.Amount);
        }

        // ---- Monthly outflow (spending) from the last 90 days of bank data ----
        var since = today.AddDays(-90);
        var monthlyOutflow = Math.Abs(bankTx
            .Where(t => t.TransactionDate >= since && t.Amount < 0
                && string.Equals(t.Currency, reporting, StringComparison.OrdinalIgnoreCase)
                && !IsInternalTransfer(t.Description))
            .Sum(t => t.Amount)) / 3m;
        var dailyBurn = monthlyOutflow / 30m;

        // ---- Predicted incoming payments ----
        var receivable = invoices
            .Where(i => i.Status is InvoiceStatus.Sent or InvoiceStatus.Overdue)
            .ToList();

        var expected = receivable.Select(inv =>
        {
            stats.TryGetValue(inv.ClientId, out var s);
            var predictedLate = s is not null ? (int)Math.Round(s.AvgDaysLate) : 0;
            var predictedDate = inv.DueDate.AddDays(predictedLate);
            if (predictedDate < today) predictedDate = today; // can't be paid in the past
            return new PredictedPaymentDto(
                inv.Id,
                inv.InvoiceNumber,
                inv.ClientId,
                clientNames.GetValueOrDefault(inv.ClientId, inv.Client.Name),
                Math.Round(Total(inv), 2),
                inv.DueDate,
                predictedDate,
                s is not null ? Confidence(s.Sample) : "New",
                s?.Score ?? "New",
                predictedDate.DayNumber - inv.DueDate.DayNumber,
                inv.DueDate < today);
        })
        .OrderBy(p => p.PredictedDate)
        .ToList();

        var inflowByDate = expected
            .GroupBy(p => p.PredictedDate)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));

        // ---- Forward projection ----
        var projection = new List<CashProjectionPointDto>();
        var bal = currentBalance;
        for (var d = 0; d <= horizon; d++)
        {
            var date = today.AddDays(d);
            var inflow = inflowByDate.GetValueOrDefault(date, 0m);
            bal += inflow;
            bal -= dailyBurn;
            projection.Add(new CashProjectionPointDto(date, Math.Round(bal, 2), inflow));
        }

        // ---- Runway ----
        int runwayDays;
        DateOnly? zeroDate = null;
        if (!hasBalance)
        {
            runwayDays = -1;
        }
        else
        {
            var zero = projection.FirstOrDefault(p => p.Balance <= 0);
            if (zero is not null)
            {
                runwayDays = zero.Date.DayNumber - today.DayNumber;
                zeroDate = zero.Date;
            }
            else if (dailyBurn > 0)
            {
                var endBal = projection[^1].Balance;
                var extra = (int)(endBal / dailyBurn);
                runwayDays = horizon + extra;
                if (runwayDays > 720) runwayDays = -1; // effectively healthy
                else zeroDate = today.AddDays(runwayDays);
            }
            else
            {
                runwayDays = -1; // no spending recorded
            }
        }

        var expectedInflow30 = expected
            .Where(p => p.PredictedDate <= today.AddDays(30))
            .Sum(p => p.Amount);

        var overdue = receivable.Where(i => i.DueDate < today).ToList();

        var reliability = stats
            .Select(kv => new ClientReliabilityDto(
                kv.Key,
                clientNames.GetValueOrDefault(kv.Key, "Client"),
                Math.Round(kv.Value.AvgDaysLate, 1),
                Math.Round(kv.Value.OnTimeRate, 2),
                kv.Value.Sample,
                kv.Value.Score))
            .OrderByDescending(c => c.AvgDaysLate)
            .ToList();

        var dto = new CashFlowProjectionDto(
            Currency: reporting,
            HasBalance: hasBalance,
            CurrentBalance: Math.Round(currentBalance, 2),
            BalanceAsOf: profile?.CashBalanceAsOf,
            MonthlyOutflow: Math.Round(monthlyOutflow, 2),
            ExpectedInflow30: Math.Round(expectedInflow30, 2),
            RunwayDays: runwayDays,
            ZeroDate: zeroDate,
            Projection: projection,
            ExpectedPayments: expected,
            ClientReliability: reliability,
            OverdueCount: overdue.Count,
            OverdueAmount: Math.Round(overdue.Sum(Total), 2));

        return HttpResult<CashFlowProjectionDto?>.Ok(dto);
    }

    private static string Score(int sample, double avgDaysLate)
    {
        if (sample < 2) return "New";
        if (avgDaysLate <= 3) return "Reliable";
        if (avgDaysLate <= 14) return "Slow";
        return "Risky";
    }

    private static string Confidence(int sample)
        => sample >= 5 ? "High" : sample >= 2 ? "Medium" : "Low";

    private static bool IsInternalTransfer(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return false;
        var d = description.ToLowerInvariant();
        return d.Contains("saving") || d.Contains("transfer") || d.Contains("own account") || d.Contains("to gbp") || d.Contains("to usd") || d.Contains("to eur");
    }
}
