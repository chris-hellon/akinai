namespace Akinai.Shared.Dtos.Invoices;

/// <summary>
/// A proposed summary, priced and ready to review.
///
/// The numbers here are already the real ones — the server grouped and summed
/// the entries before returning, so what the user reviews is what they would
/// send. Nothing is saved until they say so.
/// </summary>
/// <param name="Accepted">False when no usable summary could be produced.</param>
/// <param name="Reason">Why not, in words fit to show a user.</param>
/// <param name="Lines">The proposed lines, in order.</param>
/// <param name="OriginalRowCount">How many rows the client would otherwise read.</param>
public record TimesheetSummaryDto(
    bool Accepted,
    string? Reason,
    List<ProposedServiceLineDto> Lines,
    int OriginalRowCount)
{
    public static TimesheetSummaryDto Unavailable(string reason, int originalRowCount = 0)
        => new(false, reason, [], originalRowCount);
}

/// <param name="Description">Editable. The only field the user changes.</param>
/// <param name="TimeEntryIds">What is in this group — sent back on save.</param>
/// <param name="Hours">Summed from the entries. Shown, not edited.</param>
/// <param name="Amount">Summed from the entries. Shown, not edited.</param>
public record ProposedServiceLineDto(
    string Description,
    List<Guid> TimeEntryIds,
    decimal Hours,
    decimal Amount);
