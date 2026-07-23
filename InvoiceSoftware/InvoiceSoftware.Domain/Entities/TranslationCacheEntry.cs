using System.Security.Cryptography;
using System.Text;
using InvoiceSoftware.Domain.Common;
using InvoiceSoftware.Domain.Enums;

namespace InvoiceSoftware.Domain.Entities;

/// <summary>
/// One translated string, kept so the same text is never paid for twice.
///
/// The key is a hash of the source text plus the target language, not the
/// invoice or line item it came from. That choice does the important work:
/// editing a description changes its hash, so the stale translation is simply
/// never looked up again — there is no invalidation to remember and no way for
/// a client to be shown a translation of text the user has since rewritten.
/// It also means repeated phrases ("Design work", "Consulting") are translated
/// once across every invoice that uses them.
///
/// Not user-scoped: the cache holds machine translations of short commercial
/// phrases, and sharing them across accounts is what makes the shared Azure
/// character allowance go far enough for everyone. Nothing here is retrievable
/// without already knowing the exact source text.
/// </summary>
public class TranslationCacheEntry : BaseEntity
{
    /// <summary>SHA-256 of the source text, hex-encoded. Deterministic across processes.</summary>
    public string SourceHash { get; private set; } = null!;

    public SupportedLanguage SourceLanguage { get; private set; }
    public SupportedLanguage TargetLanguage { get; private set; }

    /// <summary>Kept for debugging and for auditing what was sent to the provider.</summary>
    public string SourceText { get; private set; } = null!;

    public string TranslatedText { get; private set; } = null!;

    public DateTime CreatedAtUtc { get; private set; }

    private TranslationCacheEntry() { }

    public static TranslationCacheEntry Create(
        string sourceText,
        string translatedText,
        SupportedLanguage sourceLanguage,
        SupportedLanguage targetLanguage,
        DateTime nowUtc)
        => new()
        {
            SourceHash = HashOf(sourceText),
            SourceText = sourceText,
            TranslatedText = translatedText,
            SourceLanguage = sourceLanguage,
            TargetLanguage = targetLanguage,
            CreatedAtUtc = nowUtc
        };

    /// <summary>
    /// The lookup key for a piece of source text. Trimmed first so trailing
    /// whitespace from a text box does not read as a different string and
    /// trigger a second paid translation of identical content.
    /// </summary>
    public static string HashOf(string sourceText)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sourceText.Trim())));
}
