using Akinai.Shared.Dtos.Invoices;

namespace Akinai.Infrastructure.Handlers.Invoices;

/// <summary>
/// Gathers the user-authored text an invoice actually shows a client, and puts
/// the translations back.
///
/// Shared by the public and the authenticated handlers so the two cannot drift:
/// an invoice must read identically whether the owner previews it or the client
/// opens the emailed link.
///
/// The rule is: translate what the client needs to understand, leave what
/// identifies a thing in the user's own system.
///
/// So the job name goes ("Implement AI prompts area" is what the work was), but
/// the project and section names do not — "Stock" and "Cash Up" are internal
/// codenames, and machine translation turns the first into "Cổ phiếu", meaning
/// company shares. Merchant names stay for the same reason: they are real
/// businesses. Expense categories are a closed enum and come from
/// <c>InvoiceLabels.ExpenseCategory</c> instead.
///
/// Note the field list is driven by what the renderer actually displays, not by
/// what reads like prose on the DTO — the services table shows JobName and
/// never touches Description.
/// </summary>
internal static class InvoiceTranslationFields
{
    public static Dictionary<string, string> Collect(
        List<InvoiceLineItemDto> lineItems,
        List<InvoiceExpenseLineItemDto> expenses,
        List<InvoiceProductLineItemDto> products,
        string? notes)
    {
        var texts = new Dictionary<string, string>();

        for (var i = 0; i < lineItems.Count; i++)
        {
            // Job name only. Project and section are identifiers, not prose.
            Add(texts, $"li:{i}:job", lineItems[i].JobName);
        }

        for (var i = 0; i < products.Count; i++)
        {
            Add(texts, $"pr:{i}:name", products[i].ProductName);
            Add(texts, $"pr:{i}:desc", products[i].Description);
        }

        Add(texts, "notes", notes);
        return texts;
    }

    public static void Apply(
        IReadOnlyDictionary<string, string> translated,
        List<InvoiceLineItemDto> lineItems,
        List<InvoiceExpenseLineItemDto> expenses,
        List<InvoiceProductLineItemDto> products,
        ref string? notes)
    {
        for (var i = 0; i < lineItems.Count; i++)
        {
            lineItems[i] = lineItems[i] with
            {
                JobName = Get(translated, $"li:{i}:job", lineItems[i].JobName)!,
            };
        }

        for (var i = 0; i < products.Count; i++)
        {
            products[i] = products[i] with
            {
                ProductName = Get(translated, $"pr:{i}:name", products[i].ProductName)!,
                Description = Get(translated, $"pr:{i}:desc", products[i].Description)!,
            };
        }

        notes = Get(translated, "notes", notes);
    }

    /// <summary>Blank fields are never sent — an empty string costs nothing but muddies the batch.</summary>
    private static void Add(IDictionary<string, string> texts, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            texts[key] = value;
    }

    /// <summary>
    /// Falls back to the original whenever a key was skipped or untranslatable,
    /// so the result is null only where the original already was.
    /// </summary>
    private static string? Get(IReadOnlyDictionary<string, string> translated, string key, string? original)
        => translated.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : original;
}
