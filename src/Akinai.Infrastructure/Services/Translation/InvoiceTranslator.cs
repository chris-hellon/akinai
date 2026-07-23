using Akinai.Domain.Entities;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Ai;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DomainLanguage = Akinai.Domain.Enums.SupportedLanguage;
using SharedLanguage = Akinai.Shared.Enums.SupportedLanguage;

namespace Akinai.Infrastructure.Services.Translation;

/// <inheritdoc />
public class InvoiceTranslator(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ITranslationService translator,
    IEntitlementService entitlements,
    IAiBudgetService budget,
    IDateTimeService clock,
    ILogger<InvoiceTranslator> logger) : IInvoiceTranslator
{
    public async Task<SharedLanguage> ResolveForInvoiceAsync(
        string userId,
        Guid clientId,
        SharedLanguage? invoiceLanguage = null,
        CancellationToken cancellationToken = default)
    {
        // The invoice's own choice wins and needs no lookup at all.
        if (invoiceLanguage is { } chosen)
            return chosen;

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // A per-client override is the more specific answer, so it wins — but
        // only when it is actually marked for invoices; the same setting also
        // covers estimates and a client may want them to differ.
        var clientSetting = await db.ClientLanguageSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.ClientId == clientId, cancellationToken);

        if (clientSetting is { UseForInvoices: true })
            return ToShared(clientSetting.PreferredLanguage);

        var userPreference = await db.UserLanguagePreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        return userPreference is null
            ? SharedLanguage.English
            : ToShared(userPreference.InvoiceLanguage);
    }

    public async Task<IReadOnlyDictionary<string, string>> TranslateAsync(
        string userId,
        SharedLanguage target,
        IReadOnlyDictionary<string, string> textsByKey,
        CancellationToken cancellationToken = default)
    {
        // Start from the originals so every key is answered no matter which of
        // the paths below we leave by.
        var results = new Dictionary<string, string>(textsByKey);

        if (target == SharedLanguage.English || textsByKey.Count == 0)
            return results;

        var pending = textsByKey
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
            .ToDictionary(kv => kv.Key, kv => kv.Value.Trim());

        if (pending.Count == 0)
            return results;

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // --- Cache first -------------------------------------------------
        // Deliberately before any entitlement or budget check. Text already
        // paid for costs nothing to serve again, so an invoice that was
        // translated while the account was on Pro keeps rendering correctly
        // even if the plan later lapses or the month's budget runs out. Only
        // *new* translation is a privilege.
        var hashesByKey = pending.ToDictionary(
            kv => kv.Key,
            kv => TranslationCacheEntry.HashOf(kv.Value));

        var wantedHashes = hashesByKey.Values.Distinct().ToList();
        var source = ToDomain(SharedLanguage.English);
        var targetDomain = ToDomain(target);

        var cached = await db.TranslationCache
            .AsNoTracking()
            .Where(e => wantedHashes.Contains(e.SourceHash)
                        && e.SourceLanguage == source
                        && e.TargetLanguage == targetDomain)
            .ToDictionaryAsync(e => e.SourceHash, e => e.TranslatedText, cancellationToken);

        // Misses are collected by HASH, not by caller key, so the request holds
        // one entry per distinct string. This matters more than it looks: an
        // invoice built from time entries repeats the same job description on
        // every row, so a 30-line invoice with three real descriptions would
        // otherwise be charged for thirty translations of the same text against
        // a character allowance shared by every account.
        var missesByHash = new Dictionary<string, string>();
        foreach (var (key, text) in pending)
        {
            var hash = hashesByKey[key];
            if (cached.TryGetValue(hash, out var hit))
                results[key] = hit;
            else
                missesByHash[hash] = text;
        }

        if (missesByHash.Count == 0)
            return results;

        // --- Everything past here costs money ----------------------------
        if (!translator.IsConfigured)
            return results;

        var plan = await entitlements.GetAsync(userId, cancellationToken);
        if (!plan.CanUseAi)
            return results;

        // A plan that restricts languages must not be able to spend on one it
        // is not entitled to, whatever the stored preference says.
        if (plan.AllowedLanguages is { Count: > 0 } allowed
            && !allowed.Contains(target.ToString(), StringComparer.OrdinalIgnoreCase))
            return results;

        var remaining = await budget.GetAsync(userId, cancellationToken);
        if (!remaining.CanSpend)
            return results;

        IReadOnlyDictionary<string, string> translated;
        try
        {
            translated = await translator.TranslateBatchAsync(
                missesByHash, target, SharedLanguage.English, cancellationToken);
        }
        catch (Exception ex)
        {
            // Fail soft: the invoice renders in English rather than not at all.
            logger.LogWarning(ex, "Invoice translation to {Target} failed; falling back to source text.", target);
            return results;
        }

        if (translated.Count == 0)
            return results;

        // --- Persist and bill --------------------------------------------
        // `translated` is keyed by hash, so each entry is one distinct string:
        // one cache row, and one string's worth of characters billed.
        var now = clock.Now;
        var charactersSent = 0;
        var newEntries = new List<TranslationCacheEntry>();

        foreach (var (hash, text) in translated)
        {
            if (!missesByHash.TryGetValue(hash, out var original) || string.IsNullOrWhiteSpace(text))
                continue;

            charactersSent += original.Length;
            newEntries.Add(TranslationCacheEntry.Create(original, text, source, targetDomain, now));
        }

        // Fan the distinct translations back out to every row that shares the
        // text. Rows whose translation failed keep the original English.
        foreach (var key in pending.Keys)
        {
            if (translated.TryGetValue(hashesByKey[key], out var text) && !string.IsNullOrWhiteSpace(text))
                results[key] = text;
        }

        if (newEntries.Count > 0)
        {
            db.TranslationCache.AddRange(newEntries);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                // Another render translated the same text concurrently and won
                // the unique index. The translation is already correct and
                // already in `results` — only the cache write is redundant.
                logger.LogDebug(ex, "Translation cache write lost a race; entry already present.");
            }
        }

        if (charactersSent > 0)
            await budget.RecordTranslationAsync(userId, AiFeature.Translation, charactersSent, cancellationToken);

        return results;
    }

    // The domain and shared enums are declared separately but deliberately kept
    // in the same order, so the numeric value is the contract between them.
    private static SharedLanguage ToShared(DomainLanguage language) => (SharedLanguage)(int)language;
    private static DomainLanguage ToDomain(SharedLanguage language) => (DomainLanguage)(int)language;
}
