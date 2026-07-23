using Akinai.Domain.Common;
using Akinai.Domain.Exceptions;

namespace Akinai.Domain.Entities;

/// <summary>
/// An append-only record of everything that has happened to an account's
/// subscription.
///
/// Money changing hands needs a paper trail that survives the current state:
/// knowing an account is on Pro today does not tell you when it started, what
/// it was charged, or who moved it. Nothing here is ever updated or deleted.
///
/// Plans and event types are stored as strings rather than enums so that a
/// historic row still reads correctly after the plan line-up changes.
/// </summary>
public class SubscriptionEvent : AggregateRoot
{
    public string UserId { get; private set; } = null!;

    /// <summary>Subscribed, Renewed, Cancelled, Resumed, Expired, PlanChanged, AdminOverride.</summary>
    public string Type { get; private set; } = null!;

    public string FromPlan { get; private set; } = null!;
    public string ToPlan { get; private set; } = null!;
    public string? Cycle { get; private set; }

    public decimal? Amount { get; private set; }
    public string? Currency { get; private set; }

    /// <summary>
    /// True when no money actually moved. Set for every event raised by the
    /// simulated provider, so test subscriptions can never be mistaken for
    /// revenue.
    /// </summary>
    public bool Simulated { get; private set; }

    /// <summary>Free text — who made an admin change, or why a charge failed.</summary>
    public string? Note { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    private SubscriptionEvent() { }

    public static SubscriptionEvent Create(
        string userId,
        string type,
        string fromPlan,
        string toPlan,
        string? cycle = null,
        decimal? amount = null,
        string? currency = null,
        bool simulated = false,
        string? note = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new DomainException("A subscription event requires a user");
        if (string.IsNullOrWhiteSpace(type))
            throw new DomainException("A subscription event requires a type");

        return new SubscriptionEvent
        {
            UserId = userId,
            Type = type,
            FromPlan = fromPlan,
            ToPlan = toPlan,
            Cycle = cycle,
            Amount = amount,
            Currency = currency,
            Simulated = simulated,
            Note = note,
            OccurredAtUtc = DateTime.UtcNow,
        };
    }
}
