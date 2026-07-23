using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Reports;
using Akinai.Shared.Dtos.Reports;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Reports;

public class GetProjectProfitabilityHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService)
    : IHandle<GetProjectProfitability, List<ProjectProfitDto>>
{
    public async Task<HttpResult<List<ProjectProfitDto>>> Handle(
        GetProjectProfitability request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<List<ProjectProfitDto>>.Unauthorized();

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

        // Only projects whose client bills in the reporting currency, so revenue (from
        // hourly rates) and expenses can be added without inventing an FX conversion.
        var projects = await db.Projects
            .Include(p => p.Client)
            .Where(p => p.Client.UserId == userId
                && p.Client.Currency.ToUpper() == reporting.ToUpper())
            .ToListAsync(cancellationToken);
        var projectIds = projects.Select(p => p.Id).ToHashSet();

        // Revenue = value of billed time (hours × effective rate) dated in the window.
        var billedTime = await db.TimeEntries
            .Include(te => te.Job).ThenInclude(j => j.Project)
            .Where(te => te.UserId == userId && te.InvoiceId != null
                && te.Date >= from && te.Date <= to
                && projectIds.Contains(te.Job.ProjectId))
            .ToListAsync(cancellationToken);

        var revenueByProject = billedTime
            .GroupBy(te => te.Job.ProjectId)
            .ToDictionary(
                g => g.Key,
                g => (Revenue: g.Sum(te => te.Hours.Value * te.Job.GetEffectiveHourlyRate()),
                      Hours: g.Sum(te => (double)te.Hours.Value)));

        // Expenses tagged to a project, in the reporting currency, dated in the window.
        var projectExpenses = await db.Expenses
            .Where(e => e.CreatedBy == userId && e.ProjectId != null
                && e.ExpenseDate >= from && e.ExpenseDate <= to
                && projectIds.Contains(e.ProjectId!.Value))
            .ToListAsync(cancellationToken);

        var expenseByProject = projectExpenses
            .Where(e => string.Equals(e.Amount.Currency, reporting, StringComparison.OrdinalIgnoreCase))
            .GroupBy(e => e.ProjectId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(e => e.GetTotalAmount()));

        var results = projects
            .Select(p =>
            {
                var rev = revenueByProject.TryGetValue(p.Id, out var r) ? r.Revenue : 0m;
                var hrs = revenueByProject.TryGetValue(p.Id, out var r2) ? r2.Hours : 0d;
                var exp = expenseByProject.TryGetValue(p.Id, out var e) ? e : 0m;
                return new ProjectProfitDto(p.Id, p.Name, p.Client.Name, rev, exp, rev - exp, hrs);
            })
            .Where(x => x.Revenue != 0 || x.Expenses != 0)
            .OrderByDescending(x => x.Profit)
            .ToList();

        return HttpResult<List<ProjectProfitDto>>.Ok(results);
    }
}
