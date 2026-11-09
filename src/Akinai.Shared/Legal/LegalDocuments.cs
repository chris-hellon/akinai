namespace Akinai.Shared.Legal;

/// <summary>
/// Which version of the Terms and Privacy Policy is currently published.
///
/// A consent record that says somebody agreed to "the terms" without saying which is worth
/// very little — the terms change, and the question in a dispute is what was on screen on
/// the day. One constant, bumped by hand when either document changes materially, is
/// enough to answer that, and is honest about the fact that a human decides what counts as
/// material.
///
/// <para>
/// Dated rather than numbered, so the version is legible on its own: "2026-11-09" needs no
/// lookup table to place in time, where "v3" does. Bump it when wording changes in a way
/// that would alter what a reasonable person thought they were agreeing to — a typo fix is
/// not that, a new sub-processor is.
/// </para>
/// </summary>
public static class LegalDocuments
{
    /// <summary>
    /// The current version of both documents. They share one version deliberately: they
    /// are accepted together in a single act, and two version numbers would invite a
    /// record that pins one and not the other.
    /// </summary>
    public const string CurrentVersion = "2026-11-09";

    /// <summary>Where the published documents live, for linking from a consent prompt.</summary>
    public const string TermsPath = "/terms";
    public const string PrivacyPath = "/privacy";
}
