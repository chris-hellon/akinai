using InvoiceSoftware.Shared.Dtos.Billing;
using DomainLanguage = InvoiceSoftware.Domain.Enums.SupportedLanguage;

namespace InvoiceSoftware.Infrastructure.Handlers.Invoices;

/// <summary>
/// Validates a language asked for when creating or editing an invoice.
///
/// The picker in the UI hides languages the plan does not include, but hiding a
/// dropdown option has never stopped a crafted request — a Free account posting
/// <c>Language: "Vietnamese"</c> directly would otherwise get a paid feature for
/// nothing, and go on to spend against the shared translation allowance.
/// </summary>
internal static class InvoiceLanguageRequest
{
    /// <summary>
    /// Parses and authorises the requested language.
    ///
    /// Null or blank resolves to null, meaning "inherit from the client or
    /// account" — that is always allowed, because it cannot select anything the
    /// account could not already use.
    /// </summary>
    /// <returns>False when the value is unrecognised or not permitted.</returns>
    public static bool TryResolve(string? requested, EntitlementsDto entitlements, out DomainLanguage? language)
    {
        language = null;

        if (string.IsNullOrWhiteSpace(requested))
            return true;

        if (!Enum.TryParse<DomainLanguage>(requested, ignoreCase: true, out var parsed))
            return false;

        // A restricted plan may only pick from its own list. An unrestricted
        // plan reports null and may pick anything.
        if (entitlements.AllowedLanguages is { Count: > 0 } allowed
            && !allowed.Contains(parsed.ToString(), StringComparer.OrdinalIgnoreCase))
            return false;

        language = parsed;
        return true;
    }
}
