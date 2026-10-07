using Akinai.Infrastructure.Data;
using Akinai.Shared.Compliance;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Invoices;

/// <summary>
/// Assembles the inputs <see cref="CorridorRules"/> needs from the database, in one place
/// so the invoice view, the client's public copy and the PDF cannot reach different
/// conclusions about the same invoice — the mistake that produced the totals divergence.
/// </summary>
public static class InvoiceCompliance
{
    /// <param name="ownerUserId">
    /// The invoice's owner. Taken explicitly rather than from the current user, because the
    /// public view runs with no signed-in user at all.
    /// </param>
    public static async Task<CorridorRules.Assessment> AssessAsync(
        ApplicationDbContext db,
        Guid invoiceId,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var invoice = await db.Invoices
            .AsNoTracking()
            .Where(i => i.Id == invoiceId)
            .Select(i => new
            {
                i.TaxRate,
                BuyerCountryCode = i.Client.BillingAddress != null ? i.Client.BillingAddress.CountryCode : null,
                i.Client.TaxNumber,
                i.Client.IsBusiness,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (invoice is null)
            return new CorridorRules.Assessment(CorridorRules.Treatment.Unknown, [], []);

        var seller = await db.BusinessProfiles
            .AsNoTracking()
            .Where(b => b.UserId == ownerUserId)
            .Select(b => new
            {
                SellerCountryCode = b.Address != null ? b.Address.CountryCode : null,
                b.TaxNumber,
            })
            .FirstOrDefaultAsync(cancellationToken);

        return CorridorRules.Assess(
            sellerCountryCode: seller?.SellerCountryCode,
            buyerCountryCode: invoice.BuyerCountryCode,
            buyerIsBusiness: invoice.IsBusiness,
            sellerTaxNumber: seller?.TaxNumber,
            buyerTaxNumber: invoice.TaxNumber,
            taxRate: invoice.TaxRate);
    }
}
