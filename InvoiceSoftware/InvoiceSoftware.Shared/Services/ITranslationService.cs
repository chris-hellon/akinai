using InvoiceSoftware.Shared.Enums;

namespace InvoiceSoftware.Shared.Services;

/// <summary>
/// Translates user-authored text — invoice line items, notes — into a client's
/// language.
///
/// This is machine translation, not an LLM: it is faster, cheaper and
/// deterministic, which is what you want for text that appears on a financial
/// document. Writing new prose (turning a scrappy time entry into a client-
/// ready description) is a different job and does not belong here.
///
/// Implementations must fail soft: a translation outage should never stop an
/// invoice being created. An empty result means "no translation available",
/// and callers fall back to the original text.
/// </summary>
public interface ITranslationService
{
    /// <summary>True when translation is configured and available.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Translates one string. Returns the translated text, or null when
    /// unavailable, the target is the source language, or the text is empty.
    /// </summary>
    Task<string?> TranslateAsync(
        string text,
        SupportedLanguage target,
        SupportedLanguage source = SupportedLanguage.English,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Translates several strings in one call, keyed so callers can match
    /// results back to their rows. Far cheaper than one call per line item —
    /// an invoice is a single request.
    ///
    /// Keys missing from the result simply had no translation; callers keep
    /// the original text for those.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> TranslateBatchAsync(
        IReadOnlyDictionary<string, string> textsByKey,
        SupportedLanguage target,
        SupportedLanguage source = SupportedLanguage.English,
        CancellationToken cancellationToken = default);
}
