using Akinai.Infrastructure.Data;

namespace Akinai.Infrastructure.Services;

/// <summary>
/// Recomputes and snapshots an invoice's totals (subtotal/tax/total in its own currency,
/// plus the total converted into every currency the owner's clients use). Operates on a
/// tracked invoice in the given context; the caller is responsible for SaveChanges.
/// </summary>
public interface IInvoiceTotalService
{
    Task RecalculateAsync(ApplicationDbContext db, Guid invoiceId, string userId, CancellationToken cancellationToken = default);
}
