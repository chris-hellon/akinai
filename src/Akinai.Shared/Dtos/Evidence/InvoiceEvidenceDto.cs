namespace Akinai.Shared.Dtos.Evidence;

/// <summary>
/// The chain of custody for one invoice: what was sent, to whom, when, what it said, and
/// what the client did with it.
///
/// Assembled from records written at the time rather than reconstructed, which is the
/// whole point — a trail generated on demand from current data proves nothing about what
/// was sent six months ago.
/// </summary>
public record InvoiceEvidenceDto(
    Guid InvoiceId,
    string InvoiceNumber,
    string ClientName,
    string Currency,
    decimal Total,
    DateOnly IssueDate,
    DateOnly DueDate,
    string Status,

    /// <summary>When the invoice was emailed, and to where.</summary>
    DateTime? SentAtUtc,
    string? SentToEmail,

    /// <summary>
    /// SHA-256 of the PDF that was attached. Proves the document has not been altered
    /// since — by either party.
    /// </summary>
    string? SentDocumentSha256,

    DateTime? FirstViewedAtUtc,
    DateTime? LastViewedAtUtc,
    int ViewCount,

    /// <summary>The tax treatment and wording the document carried when assessed.</summary>
    string TaxTreatment,
    IReadOnlyList<string> RequiredStatements,

    DateTime? SignedAtUtc,
    string? SignedByName,
    string? SignedByEmail,

    /// <summary>
    /// Where the signature was given from. Weak on its own and decisive alongside the
    /// rest — it is the one part of the trail the signer cannot later claim was typed
    /// by somebody else.
    /// </summary>
    string? SignedFromIpAddress,

    /// <summary>
    /// Every reminder actually sent for this invoice, oldest first. Part of the trail
    /// rather than a footnote to it: "they were chased four times and never replied" is
    /// the claim a late-payment dispute turns on, and these rows are the only record of
    /// it. Empty when nothing was ever sent.
    /// </summary>
    IReadOnlyList<InvoiceEvidenceReminderDto> Reminders);

/// <summary>One reminder, as it went out.</summary>
public record InvoiceEvidenceReminderDto(
    /// <summary>The <c>DunningStage</c> name, stored as text on the reminder.</summary>
    string Stage,
    DateTime SentAtUtc,
    string ToEmail,
    /// <summary>The language it was written in, which is itself part of the record.</summary>
    string Language);
