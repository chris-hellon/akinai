namespace InvoiceSoftware.Shared.Services;

/// <summary>How much of this month's AI allowance an account has left.</summary>
/// <param name="LimitUsd">Null when the plan has no AI allowance at all.</param>
/// <param name="SpentUsd">Month-to-date, summed from recorded usage.</param>
public record AiBudget(decimal? LimitUsd, decimal SpentUsd)
{
    public bool HasAllowance => LimitUsd is > 0;

    public decimal RemainingUsd => LimitUsd is decimal limit ? Math.Max(0, limit - SpentUsd) : 0m;

    /// <summary>
    /// True while there is budget left. Deliberately not "remaining >= estimated
    /// cost" — a single call costs a fraction of a cent, so any remaining
    /// balance is enough for one more, and the overshoot is bounded by the
    /// per-call token cap rather than by pre-flight estimation.
    /// </summary>
    public bool CanSpend => HasAllowance && SpentUsd < LimitUsd;

    public int PercentUsed => LimitUsd is decimal limit && limit > 0
        ? (int)Math.Min(100, Math.Round(SpentUsd / limit * 100))
        : 0;
}

/// <summary>
/// Enforces the per-account monthly AI spend cap and records what was spent.
///
/// The cap exists to bound the damage from a runaway loop or a scripted abuser,
/// not because AI is expected to be a material cost — at Haiku rates a typical
/// account spends single-digit pence a month. Treat it as a fuse, not a meter.
/// </summary>
public interface IAiBudgetService
{
    /// <summary>This month's allowance and spend for the account.</summary>
    Task<AiBudget> GetAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a completed model call and returns the budget after it.
    /// Call this with the provider's reported token usage, never an estimate.
    /// </summary>
    Task<AiBudget> RecordTokensAsync(
        string userId, string feature, string model,
        int inputTokens, int outputTokens, CancellationToken cancellationToken = default);

    /// <summary>Records a completed translation, priced on source characters.</summary>
    Task<AiBudget> RecordTranslationAsync(
        string userId, string feature, int characters, CancellationToken cancellationToken = default);
}
