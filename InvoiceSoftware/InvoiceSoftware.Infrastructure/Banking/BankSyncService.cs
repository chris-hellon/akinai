using InvoiceSoftware.Domain.Entities;
using InvoiceSoftware.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InvoiceSoftware.Infrastructure.Banking;

/// <summary>
/// Pulls new transactions and the current balance for a connection into the app, de-duplicating
/// on the provider's stable transaction id. Reused by the sync endpoint and any background sync.
/// </summary>
public class BankSyncService(IBankProviderRegistry registry, ILogger<BankSyncService> logger)
{
    public async Task<int> SyncAsync(BankConnection connection, ApplicationDbContext db, CancellationToken cancellationToken)
    {
        if (connection.ExternalAccountId is null)
            return 0;

        var provider = registry.ByName(connection.Provider);
        if (provider is null)
        {
            connection.MarkError($"Provider '{connection.Provider}' is not available.");
            await db.SaveChangesAsync(cancellationToken);
            return 0;
        }

        try
        {
            // Small overlap window so nothing slips between syncs.
            var from = connection.LastSyncedAt is { } ls ? DateOnly.FromDateTime(ls.AddDays(-5)) : (DateOnly?)null;
            var transactions = await provider.GetTransactionsAsync(connection, from, cancellationToken);

            var existing = (await db.BankTransactions
                    .Where(bt => bt.UserId == connection.UserId && bt.ExternalId != null)
                    .Select(bt => bt.ExternalId!)
                    .ToListAsync(cancellationToken))
                .ToHashSet(StringComparer.Ordinal);

            var imported = 0;
            foreach (var t in transactions)
            {
                if (existing.Contains(t.ExternalId)) continue;
                db.BankTransactions.Add(BankTransaction.CreateFromFeed(
                    connection.UserId, t.ExternalId, t.Date, t.Description, t.Amount, t.Currency, t.Reference, connection.InstitutionName));
                existing.Add(t.ExternalId);
                imported++;
            }

            // Live balance feeds the cash-flow forecast anchor.
            var balance = await provider.GetBalanceAsync(connection, cancellationToken);
            if (balance is not null)
            {
                var profile = await db.BusinessProfiles.FirstOrDefaultAsync(bp => bp.UserId == connection.UserId, cancellationToken);
                profile?.SetCashBalance(balance.Amount, DateOnly.FromDateTime(DateTime.UtcNow));
            }

            connection.MarkSynced(DateTime.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
            return imported;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Bank sync failed for connection {ConnectionId}", connection.Id);
            connection.MarkError(ex.Message);
            await db.SaveChangesAsync(cancellationToken);
            throw;
        }
    }
}
