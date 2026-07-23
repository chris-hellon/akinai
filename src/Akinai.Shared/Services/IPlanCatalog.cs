using Akinai.Shared.Billing;

namespace Akinai.Shared.Services;

/// <summary>
/// The plan catalogue as it currently stands.
///
/// Everything that gates a feature or shows a price reads from here, so the
/// pricing page and the server-side checks cannot drift apart — that guarantee
/// is why this replaced the static <c>Plans</c> class rather than sitting
/// alongside it.
///
/// Implementations must never fail open. If the stored settings are missing or
/// unreadable, resolve to the compiled defaults in <see cref="Plans"/>: an
/// outage that silently granted every paid feature would be worse than one
/// that showed slightly stale prices.
/// </summary>
public interface IPlanCatalog
{
    Task<PlanDefinition> ForAsync(BillingPlan plan, CancellationToken cancellationToken = default);

    /// <summary>Every tier, in display order.</summary>
    Task<IReadOnlyList<PlanDefinition>> AllAsync(CancellationToken cancellationToken = default);

    /// <summary>Drops the cache after an edit so the next read is authoritative.</summary>
    void Invalidate();
}
