using Akinai.Domain.Common;
using Akinai.Domain.Exceptions;

namespace Akinai.Domain.Entities;

/// <summary>
/// A reminder that was actually sent for an overdue invoice.
///
/// Written as the send happens, not planned in advance, because the schedule that decides
/// what to send next is a pure function of what has already gone out. If a send were
/// recorded optimistically and then failed, the client would silently skip that rung; if
/// it were recorded late and the process died, they would get the same email twice. The
/// row is therefore the record of fact, and the only thing the schedule trusts.
/// </summary>
public class DunningReminder : BaseEntity
{
    public Guid InvoiceId { get; private set; }

    /// <summary>
    /// Denormalised from the invoice on purpose. The schedule needs every reminder sent to
    /// this client <em>today</em> across all their invoices — four late invoices are one
    /// relationship, not four — and that question is asked far more often than it changes.
    /// </summary>
    public Guid ClientId { get; private set; }

    public string UserId { get; private set; } = null!;

    /// <summary>The <c>DunningStage</c> name. Stored as text so a renumbered enum cannot
    /// silently re-point historical rows at a different rung.</summary>
    public string Stage { get; private set; } = null!;

    public DateTime SentAtUtc { get; private set; }
    public string ToEmail { get; private set; } = null!;

    /// <summary>The language it went out in, for the record rather than for the schedule.</summary>
    public string Language { get; private set; } = null!;

    private DunningReminder() { }

    public static DunningReminder Record(
        Guid invoiceId, Guid clientId, string userId,
        string stage, DateTime sentAtUtc, string toEmail, string language)
    {
        if (string.IsNullOrWhiteSpace(stage))
            throw new DomainException("A reminder must record which stage was sent.");
        if (string.IsNullOrWhiteSpace(toEmail))
            throw new DomainException("A reminder must record where it was sent.");

        return new DunningReminder
        {
            InvoiceId = invoiceId,
            ClientId = clientId,
            UserId = userId,
            Stage = stage,
            SentAtUtc = sentAtUtc,
            ToEmail = toEmail,
            Language = language,
        };
    }
}
