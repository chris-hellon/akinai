using Akinai.Shared.Enums;

namespace Akinai.Shared.Services;

/// <summary>
/// Decides what language an invoice should be written in, and translates the
/// freeform parts of it into that language.
///
/// Only freeform text goes through here — descriptions, notes, the things the
/// user typed. The document's fixed headings come from
/// <see cref="Localization.InvoiceLabels"/>, which is free, instant and correct.
///
/// Every method fails soft. A translation outage, an exhausted budget or a
/// downgraded plan all produce the original English text, never an error: an
/// invoice must always render, and an untranslated invoice is a far smaller
/// problem than one a client cannot open.
/// </summary>
public interface IInvoiceTranslator
{
    /// <summary>
    /// The language an invoice should be written in, most specific first: the
    /// invoice's own choice, then the client's override, then the account
    /// default, then English.
    ///
    /// <paramref name="invoiceLanguage"/> is what makes an issued invoice
    /// stable — once an invoice records its language, changing the account
    /// default must not rewrite a document the client already has.
    /// </summary>
    Task<SupportedLanguage> ResolveForInvoiceAsync(
        string userId,
        Guid clientId,
        SupportedLanguage? invoiceLanguage = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Translates keyed strings into <paramref name="target"/>.
    ///
    /// Always returns an entry for every input key. Anything that could not be
    /// translated — because the plan has no AI allowance, the monthly budget is
    /// spent, translation is unconfigured, or the provider failed — comes back
    /// as the original text.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> TranslateAsync(
        string userId,
        SupportedLanguage target,
        IReadOnlyDictionary<string, string> textsByKey,
        CancellationToken cancellationToken = default);
}
