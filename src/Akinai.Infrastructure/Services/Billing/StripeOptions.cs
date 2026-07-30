namespace Akinai.Infrastructure.Services.Billing;

/// <summary>
/// Stripe credentials.
///
/// All three are secrets and live in appsettings.json, which is gitignored —
/// never in the example file, and never in source. Use the test-mode keys
/// (<c>sk_test_…</c>, <c>whsec_…</c>) until you have run a real card through it.
/// </summary>
public class StripeOptions
{
    public const string SectionName = "Stripe";

    /// <summary>Secret API key. <c>sk_test_…</c> in sandbox, <c>sk_live_…</c> in production.</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Webhook signing secret, from the endpoint's page in the Stripe dashboard
    /// or printed by <c>stripe listen</c>.
    ///
    /// Without it the webhook cannot be verified, and an unverified webhook is
    /// an open endpoint that grants paid plans to anyone who can POST to it.
    /// The handler refuses to run rather than trusting an unsigned payload.
    /// </summary>
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>
    /// Configured and safe to use. Both the key and the webhook secret are
    /// required — a provider that can take money but cannot confirm it would
    /// charge customers and never upgrade them.
    /// </summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(SecretKey) && !string.IsNullOrWhiteSpace(WebhookSecret);

    /// <summary>True for test-mode keys. Drives the "no money moves" warning in the UI.</summary>
    public bool IsTestMode => SecretKey.StartsWith("sk_test_", StringComparison.Ordinal);
}
