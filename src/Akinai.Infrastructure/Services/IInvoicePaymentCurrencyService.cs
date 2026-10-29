using Akinai.Infrastructure.Data;

namespace Akinai.Infrastructure.Services;

/// <summary>
/// Records which currencies an invoice accepts payment in, converting each at the rate
/// in force when it is saved.
/// </summary>
public interface IInvoicePaymentCurrencyService
{
    /// <param name="currencies">
    /// Currency codes the owner chose, excluding the invoice's own. Null leaves the
    /// existing selection alone — an update that does not mention payment currencies
    /// must not silently clear them. An empty list clears it.
    /// </param>
    Task SetAsync(
        ApplicationDbContext db,
        Guid invoiceId,
        string userId,
        IReadOnlyList<string>? currencies,
        CancellationToken cancellationToken = default);
}
