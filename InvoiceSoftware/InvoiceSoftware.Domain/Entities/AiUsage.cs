using InvoiceSoftware.Domain.Common;
using InvoiceSoftware.Domain.Exceptions;

namespace InvoiceSoftware.Domain.Entities;

/// <summary>
/// One AI or translation call, priced.
///
/// Append-only, like <see cref="SubscriptionEvent"/>. This is what makes the
/// monthly spend cap enforceable: the cap is a SUM over these rows for the
/// current month, so it cannot drift from what was actually spent the way a
/// running counter on the user row would.
///
/// Rows are written *after* the call returns, using the provider's reported
/// usage — never an estimate. A call that fails writes no row and costs the
/// user nothing.
/// </summary>
public class AiUsage : AggregateRoot
{
    public string UserId { get; private set; } = null!;

    /// <summary>See <c>AiFeature</c> — Translation, LineItemWriting.</summary>
    public string Feature { get; private set; } = null!;

    /// <summary>Model ID, or "azure-translator" for machine translation.</summary>
    public string Model { get; private set; } = null!;

    /// <summary>Token counts for LLM calls; zero for translation.</summary>
    public int InputTokens { get; private set; }
    public int OutputTokens { get; private set; }

    /// <summary>Source characters for translation calls; zero for LLM calls.</summary>
    public int Characters { get; private set; }

    /// <summary>
    /// USD, priced from the provider's own usage numbers. High precision
    /// because a single call can cost a small fraction of a cent — rounding to
    /// four places here would floor most rows to zero and make the cap useless.
    /// </summary>
    public decimal CostUsd { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    private AiUsage() { }

    public static AiUsage ForTokens(
        string userId, string feature, string model, int inputTokens, int outputTokens, decimal costUsd)
        => Create(userId, feature, model, inputTokens, outputTokens, characters: 0, costUsd);

    public static AiUsage ForTranslation(
        string userId, string feature, int characters, decimal costUsd)
        => Create(userId, feature, "azure-translator", 0, 0, characters, costUsd);

    private static AiUsage Create(
        string userId, string feature, string model,
        int inputTokens, int outputTokens, int characters, decimal costUsd)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new DomainException("AI usage requires a user");
        if (string.IsNullOrWhiteSpace(feature))
            throw new DomainException("AI usage requires a feature");
        if (costUsd < 0)
            throw new DomainException("AI usage cannot have a negative cost");

        return new AiUsage
        {
            UserId = userId,
            Feature = feature,
            Model = model,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            Characters = characters,
            CostUsd = costUsd,
            OccurredAtUtc = DateTime.UtcNow,
        };
    }
}
