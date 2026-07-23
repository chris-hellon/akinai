using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Dashboard;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Dashboard;

public class GetReportingCurrenciesHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService)
    : IHandle<GetReportingCurrencies, List<string>>
{
    public async Task<HttpResult<List<string>>> Handle(GetReportingCurrencies request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<List<string>>.Ok(new List<string> { "USD" });

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var clientCurrencies = await db.Clients
            .Where(c => c.UserId == userId)
            .Select(c => c.Currency)
            .Distinct()
            .ToListAsync(cancellationToken);
        foreach (var c in clientCurrencies)
            if (!string.IsNullOrWhiteSpace(c)) set.Add(c.ToUpperInvariant());

        var defaultCurrency = await db.BusinessProfiles
            .Where(b => b.UserId == userId)
            .Select(b => b.DefaultCurrency)
            .FirstOrDefaultAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(defaultCurrency))
            set.Add(defaultCurrency.ToUpperInvariant());

        if (set.Count == 0)
            set.Add("USD");

        return HttpResult<List<string>>.Ok(set.OrderBy(x => x).ToList());
    }
}
