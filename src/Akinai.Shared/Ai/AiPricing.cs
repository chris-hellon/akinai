namespace Akinai.Shared.Ai;

/// <summary>What an AI call was spent on. Stored as a string on the usage row.</summary>
public static class AiFeature
{
    /// <summary>Translating invoice or estimate line items via Azure Translator.</summary>
    public const string Translation = "Translation";

    /// <summary>Turning raw time entries into client-ready invoice descriptions.</summary>
    public const string LineItemWriting = "LineItemWriting";
}

/// <summary>
/// Per-unit costs, used to price a call after it returns.
///
/// Held in code rather than config so a rate change is a reviewed diff — these
/// numbers decide whether a plan is profitable, and a typo in an appsettings
/// file would silently under-report spend.
///
/// Verify against current provider pricing before launch; both are quoted in
/// USD, which is why <see cref="EntitlementsDto"/> reports the AI budget in USD
/// rather than the plan's billing currency.
/// </summary>
public static class AiPricing
{
    /// <summary>Anthropic model IDs. Haiku is the default: this workload is short and high-volume.</summary>
    public const string HaikuModel = "claude-haiku-4-5";
    public const string SonnetModel = "claude-sonnet-5";

    /// <summary>USD per input/output token, from the published per-million rates.</summary>
    private static readonly IReadOnlyDictionary<string, (decimal InputPerToken, decimal OutputPerToken)> TokenRates =
        new Dictionary<string, (decimal, decimal)>
        {
            // Haiku 4.5: $1.00 / $5.00 per million
            [HaikuModel] = (1.00m / 1_000_000m, 5.00m / 1_000_000m),
            // Sonnet 5: $3.00 / $15.00 per million
            [SonnetModel] = (3.00m / 1_000_000m, 15.00m / 1_000_000m),
        };

    /// <summary>
    /// Azure Translator bills per character of source text, not per token.
    ///
    /// Akinai is on the F0 free tier, which costs nothing but is capped at
    /// ~2M characters per month **across the whole account** — a shared pool,
    /// not per user. So this rate is notional: it exists to make one account's
    /// share of that pool measurable, which is what stops a single user
    /// exhausting the allowance for everyone. If the tier ever changes to
    /// pay-as-you-go (~$10/M characters) this number becomes real, and it
    /// already matches, so nothing needs to change.
    /// </summary>
    public const decimal TranslatorUsdPerCharacter = 10.00m / 1_000_000m;

    /// <summary>Cost of one model call. Unknown models price at zero rather than throwing.</summary>
    public static decimal ForTokens(string model, int inputTokens, int outputTokens)
        => TokenRates.TryGetValue(model, out var rate)
            ? (inputTokens * rate.InputPerToken) + (outputTokens * rate.OutputPerToken)
            : 0m;

    /// <summary>Cost of translating <paramref name="characterCount"/> characters.</summary>
    public static decimal ForTranslation(int characterCount)
        => characterCount * TranslatorUsdPerCharacter;
}
