namespace Akinai.Infrastructure.Services.Ai;

/// <summary>
/// Anthropic API credentials.
///
/// The key is a secret and lives in appsettings.json, which is gitignored —
/// never in the example file, and never in source.
/// </summary>
public class ClaudeOptions
{
    public const string SectionName = "Claude";

    /// <summary>API key, <c>sk-ant-…</c>. Empty disables every AI feature that needs it.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Model id. Defaults to Haiku: this workload is short, structured and
    /// high-volume, which is exactly what the cheapest tier is for. Anything
    /// set here must also exist in <c>AiPricing</c>, or calls will be recorded
    /// as costing nothing and the spend cap will not bind.
    /// </summary>
    public string Model { get; set; } = Shared.Ai.AiPricing.HaikuModel;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
