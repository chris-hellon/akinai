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
    string? SignedByName);
