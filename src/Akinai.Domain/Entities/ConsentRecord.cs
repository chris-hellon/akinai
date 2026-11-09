using Akinai.Domain.Common;
using Akinai.Domain.Exceptions;

namespace Akinai.Domain.Entities;

/// <summary>What a consent record is about. Stored as text; see the entity.</summary>
public enum ConsentPurpose
{
    /// <summary>
    /// Accepting the Terms of Service and the Privacy Policy. Required to hold an account
    /// at all, which is why it is recorded rather than inferred — "they must have agreed,
    /// they have an account" is not evidence of anything.
    /// </summary>
    TermsAndPrivacy = 0,

    /// <summary>
    /// Product and marketing email. Genuinely optional, and recorded separately for that
    /// reason: bundling it with the terms would make neither of them freely given.
    /// </summary>
    ProductEmails = 1,
}

/// <summary>
/// One act of consent, kept so the question "what did this person agree to, and when" has
/// an answer.
///
/// <para>
/// Vietnam's Personal Data Protection Law requires consent to be explicit, separable and
/// <em>demonstrable</em> — and the UK and EU rules say the same. Before this, registration
/// carried a line of small print reading "By signing up, you agree to our…", which fails
/// all three: nothing was ticked, everything was bundled, and nothing was written down. An
/// account existing is not evidence that its owner agreed to anything.
/// </para>
///
/// <para>
/// Append-only. Withdrawing consent writes a new row with <see cref="Granted"/> false
/// rather than deleting or updating the old one, because the history is the evidence: what
/// matters in a dispute is what was true at the time a thing was sent, not what is true
/// now.
/// </para>
/// </summary>
public class ConsentRecord : BaseEntity
{
    public string UserId { get; private set; } = null!;

    /// <summary>
    /// Stored as text so a renumbered enum cannot silently re-point historical rows at a
    /// different purpose — the same reason <see cref="DunningReminder.Stage"/> is text.
    /// </summary>
    public string Purpose { get; private set; } = null!;

    /// <summary>True when consent was given, false when it was withdrawn.</summary>
    public bool Granted { get; private set; }

    /// <summary>
    /// Which version of the documents was on screen. Without it the record says somebody
    /// agreed to "the terms" without saying which, and the terms change.
    /// </summary>
    public string DocumentVersion { get; private set; } = null!;

    public DateTime RecordedAtUtc { get; private set; }

    /// <summary>
    /// Where the action came from — "registration", "settings". Narrows what a reader has
    /// to reconstruct to understand a row.
    /// </summary>
    public string Source { get; private set; } = null!;

    /// <summary>
    /// The address the consent was given from, when it was captured.
    ///
    /// Nullable and best-effort: it is corroborating evidence, not the consent itself, and
    /// a proxy or a missing header must never be a reason to refuse a registration. It is
    /// also personal data in its own right, which is why nothing depends on it.
    /// </summary>
    public string? IpAddress { get; private set; }

    private ConsentRecord() { }

    public static ConsentRecord Record(
        string userId,
        ConsentPurpose purpose,
        bool granted,
        string documentVersion,
        DateTime recordedAtUtc,
        string source,
        string? ipAddress = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new DomainException("A consent record must belong to a user.");
        if (string.IsNullOrWhiteSpace(documentVersion))
            throw new DomainException("A consent record must say which version was agreed to.");
        if (string.IsNullOrWhiteSpace(source))
            throw new DomainException("A consent record must say where it came from.");

        return new ConsentRecord
        {
            UserId = userId,
            Purpose = purpose.ToString(),
            Granted = granted,
            DocumentVersion = documentVersion,
            RecordedAtUtc = recordedAtUtc,
            Source = source,
            IpAddress = ipAddress,
        };
    }
}
