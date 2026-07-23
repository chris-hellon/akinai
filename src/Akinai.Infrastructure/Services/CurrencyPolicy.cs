using Akinai.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Services;

/// <summary>
/// The "one currency" rule for the free plan, in one place.
///
/// Currency is not a single field somewhere — it leaks in through clients, the
/// business profile default, and the per-currency bank details. Gating only one
/// of those is no gate at all: set the client to GBP, then simply change the
/// default currency, or add bank details for a second currency and invoice in
/// that. So the rule is expressed once, against everything that carries a
/// currency, and every write path asks the same question.
///
/// The rule: a free account may use exactly one currency. Which one is not
/// configured — it is whichever the account already uses, so there is nothing
/// to set up and nothing to keep in step.
/// </summary>
public static class CurrencyPolicy
{
    /// <summary>Every currency this account already has data in.</summary>
    public static async Task<HashSet<string>> InUseAsync(
        ApplicationDbContext db, string userId, CancellationToken cancellationToken)
    {
        var fromClients = await db.Clients
            .Where(c => c.UserId == userId)
            .Select(c => c.Currency)
            .Distinct()
            .ToListAsync(cancellationToken);

        var fromBankDetails = await db.CurrencyPaymentSettings
            .Where(c => c.UserId == userId)
            .Select(c => c.CurrencyCode)
            .Distinct()
            .ToListAsync(cancellationToken);

        var fromProfile = await db.BusinessProfiles
            .Where(p => p.UserId == userId)
            .Select(p => p.DefaultCurrency)
            .ToListAsync(cancellationToken);

        return fromClients
            .Concat(fromBankDetails)
            .Concat(fromProfile)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!.ToUpperInvariant())
            .ToHashSet();
    }

    /// <summary>
    /// True if using <paramref name="currency"/> would not introduce a second
    /// one. An account with no currency yet is choosing its first, which is
    /// always allowed.
    ///
    /// Note this only ever blocks *new* currencies. An account that already has
    /// several — because it downgraded, or predates the plan — keeps full access
    /// to all of them; it simply cannot add another.
    /// </summary>
    public static async Task<bool> AllowsAsync(
        ApplicationDbContext db, string userId, string? currency, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(currency)) return true;

        var inUse = await InUseAsync(db, userId, cancellationToken);
        return inUse.Count == 0 || inUse.Contains(currency.ToUpperInvariant());
    }
}
