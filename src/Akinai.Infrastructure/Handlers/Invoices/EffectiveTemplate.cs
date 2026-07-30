using Akinai.Domain.Entities;
using Akinai.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Invoices;

/// <summary>
/// Resolves the template a document should actually render with.
///
/// An invoice or estimate may not name a template — most never do. When it
/// doesn't, the document must fall back to the account's <em>default</em>
/// template, not to a hardcoded standard layout: a user who set a centred,
/// logo-bearing default expects to see it on every invoice, including the ones
/// they created before choosing it.
///
/// Shared by the owner view, the public view and the PDF so all three agree —
/// the client, the sender and the printout must see one document.
/// </summary>
internal static class EffectiveTemplate
{
    /// <summary>
    /// The explicitly-linked template, or the owner's default, or null when the
    /// account has no default either (brand-new account) — callers treat null
    /// as "use the built-in defaults".
    /// </summary>
    public static async Task<InvoiceTemplate?> ResolveAsync(
        ApplicationDbContext db,
        string ownerId,
        InvoiceTemplate? linked,
        CancellationToken cancellationToken)
    {
        if (linked is not null) return linked;

        return await db.InvoiceTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(
                t => t.UserId == ownerId && t.IsDefault, cancellationToken);
    }
}
