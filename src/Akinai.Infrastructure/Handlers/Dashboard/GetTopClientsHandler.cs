using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.Enums;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Dashboard;
using Akinai.Shared.Dtos.Dashboard;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Dashboard;

public class GetTopClientsHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService)
    : IHandle<GetTopClients, List<TopClientDto>>
{
    public async Task<HttpResult<List<TopClientDto>>> Handle(
        GetTopClients request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<List<TopClientDto>>.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var now = DateOnly.FromDateTime(DateTime.UtcNow);
        var periodStart = request.Period switch
        {
            "week" => now.AddDays(-7),
            "month" => now.AddMonths(-1),
            "quarter" => now.AddMonths(-3),
            _ => now.AddYears(-1) // default to year
        };
        var count = request.Count > 0 ? request.Count : 5;

        // Get clients with their invoices
        var clients = await db.Clients
            .Where(c => c.UserId == userId)
            .Include(c => c.Projects)
                .ThenInclude(p => p.Jobs)
                    .ThenInclude(j => j.TimeEntries)
                        .ThenInclude(te => te.Invoice)
            .ToListAsync(cancellationToken);

        var clientStats = new List<TopClientDto>();

        foreach (var client in clients)
        {
            decimal totalRevenue = 0;
            decimal outstandingAmount = 0;
            int invoiceCount = 0;

            var invoices = client.Projects
                .SelectMany(p => p.Jobs)
                .SelectMany(j => j.TimeEntries)
                .Where(te => te.Invoice != null)
                .Select(te => te.Invoice!)
                .Distinct()
                .ToList();

            foreach (var invoice in invoices)
            {
                var timeEntries = invoice.TimeEntries.ToList();
                decimal subtotal = 0;
                foreach (var te in timeEntries)
                {
                    var hourlyRate = te.Job.GetEffectiveHourlyRate();
                    subtotal += te.Hours.Value * hourlyRate;
                }
                var total = subtotal + (subtotal * invoice.TaxRate / 100);

                if (invoice.Status == InvoiceStatus.Paid &&
                    invoice.PaidDate.HasValue &&
                    invoice.PaidDate.Value >= periodStart)
                {
                    totalRevenue += total;
                    invoiceCount++;
                }
                else if (invoice.Status == InvoiceStatus.Sent || invoice.Status == InvoiceStatus.Overdue)
                {
                    outstandingAmount += total;
                }
            }

            if (totalRevenue > 0 || outstandingAmount > 0)
            {
                clientStats.Add(new TopClientDto(
                    client.Id,
                    client.Name,
                    client.CompanyName,
                    totalRevenue,
                    invoiceCount,
                    outstandingAmount,
                    client.Currency));
            }
        }

        var topClients = clientStats
            .OrderByDescending(c => c.TotalRevenue)
            .Take(count)
            .ToList();

        return HttpResult<List<TopClientDto>>.Ok(topClients);
    }
}
