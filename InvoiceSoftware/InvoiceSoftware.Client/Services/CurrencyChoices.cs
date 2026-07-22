using InvoiceSoftware.Shared.Enums;

namespace InvoiceSoftware.Client.Services;

/// <summary>
/// Which currencies a picker should offer, given the plan.
///
/// One helper rather than the same filter written into each form: the app
/// offers a currency choice in six different places, and the first attempt at
/// this restricted two of them and left four showing everything. Anything that
/// lets a user pick a currency should call this.
/// </summary>
public static class CurrencyChoices
{
    /// <summary>True when the plan narrows the list, so a note is worth showing.</summary>
    public static bool IsRestricted(EntitlementState state)
        => state.Current?.AllowedCurrencies is { Count: > 0 };

    /// <summary>
    /// The codes to offer. <paramref name="currentValue"/> is always included
    /// even if the plan would not allow it now — otherwise opening a form could
    /// silently rewrite a value that was saved earlier, or on a higher plan.
    /// </summary>
    public static IReadOnlyList<string> Codes(EntitlementState state, string? currentValue = null)
    {
        var all = Enum.GetValues<Currency>().Select(c => c.ToString()).ToList();
        return Filter(state, all, c => c, currentValue);
    }

    /// <summary>Enum overload, for the forms that bind to <see cref="Currency"/>.</summary>
    public static IReadOnlyList<Currency> Values(EntitlementState state, string? currentValue = null)
        => Filter(state, Enum.GetValues<Currency>().ToList(), c => c.ToString(), currentValue);

    /// <summary>Generic overload for lists of richer currency objects.</summary>
    public static List<T> Filter<T>(
        EntitlementState state, List<T> source, Func<T, string> codeOf, string? currentValue = null)
    {
        var allowed = state.Current?.AllowedCurrencies;

        // Unrestricted, or restricted-but-undecided (no currency chosen yet, so
        // the next choice becomes the one) — offer everything.
        if (allowed is not { Count: > 0 }) return source;

        return source
            .Where(x => allowed.Contains(codeOf(x)) || codeOf(x) == currentValue)
            .ToList();
    }
}
