using Akinai.Infrastructure.Data;
using Akinai.Shared.Geography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Akinai.Infrastructure.Services;

/// <summary>
/// One-time pass that reads an ISO country code out of addresses captured before there was
/// a code to store.
///
/// Deliberately additive and conservative. The free-text country is never modified, and a
/// code is written only where <see cref="Countries.TryParse"/> is confident — "United
/// Kingdom", "UK" and "GB" all resolve, "Somewhere in Europe" does not. Anything it cannot
/// read is left null, which the compliance rules report as "country not set" rather than
/// guessing around. A wrong country silently produces a wrong tax treatment, so a blank
/// field somebody is asked to fill in is the better failure.
///
/// Rows that already have a code are skipped, so this is safe to run repeatedly.
/// </summary>
public class CountryCodeBackfillService(
    IServiceScopeFactory scopeFactory,
    ILogger<CountryCodeBackfillService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Let the app finish starting (and the schema apply) before we touch the DB.
            await Task.Delay(TimeSpan.FromSeconds(14), stoppingToken);

            using var scope = scopeFactory.CreateScope();
            var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var db = await dbFactory.CreateDbContextAsync(stoppingToken);

            var filled = 0;
            var unresolved = new List<string>();

            var clients = await db.Clients
                .Where(c => c.BillingAddress != null && c.BillingAddress.CountryCode == null)
                .ToListAsync(stoppingToken);

            foreach (var client in clients)
            {
                var free = client.BillingAddress!.Country;
                var code = Countries.TryParse(free);
                if (code is null)
                {
                    if (!string.IsNullOrWhiteSpace(free)) unresolved.Add(free);
                    continue;
                }

                client.SetBillingCountryCode(code);
                filled++;
            }

            var profiles = await db.BusinessProfiles
                .Where(b => b.Address != null && b.Address.CountryCode == null)
                .ToListAsync(stoppingToken);

            foreach (var profile in profiles)
            {
                var free = profile.Address!.Country;
                var code = Countries.TryParse(free);
                if (code is null)
                {
                    if (!string.IsNullOrWhiteSpace(free)) unresolved.Add(free);
                    continue;
                }

                profile.SetCountryCode(code);
                filled++;
            }

            if (filled > 0 || unresolved.Count > 0)
            {
                await db.SaveChangesAsync(stoppingToken);
                logger.LogInformation(
                    "Country code backfill: resolved {Filled} address(es).{Unresolved}",
                    filled,
                    unresolved.Count == 0
                        ? string.Empty
                        : $" Could not read a country from: {string.Join(", ", unresolved.Distinct())} — set these on the record.");
            }
        }
        catch (OperationCanceledException)
        {
            // App shutting down.
        }
        catch (Exception ex)
        {
            // Best-effort: a failure here must never stop the app from starting.
            logger.LogWarning(ex, "Country code backfill did not complete.");
        }
    }
}
