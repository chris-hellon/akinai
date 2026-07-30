namespace Akinai.Shared.Services;

/// <summary>One tracked sitting, as the summariser needs to see it.</summary>
/// <param name="TimeEntryId">Identity. The model returns these to say what it grouped.</param>
/// <param name="JobName">The job the time was booked against.</param>
/// <param name="ProjectName">Its project, for context — never shown to the client.</param>
/// <param name="Description">Whatever the user typed on the entry, if anything.</param>
/// <param name="Date">When the work happened.</param>
/// <param name="BillableHours">Hours to bill. Sent for context only; never returned.</param>
public record TimesheetEntry(
    Guid TimeEntryId,
    string JobName,
    string ProjectName,
    string? Description,
    DateOnly Date,
    decimal BillableHours);

/// <summary>A proposed grouping, before it is validated and priced.</summary>
public record ProposedServiceLine(string Description, IReadOnlyList<Guid> TimeEntryIds);

/// <summary>The outcome of asking for a summary.</summary>
/// <param name="Groups">Empty when nothing usable came back.</param>
/// <param name="Reason">Why it is empty, in words fit to show a user.</param>
public record TimesheetSummary(IReadOnlyList<ProposedServiceLine> Groups, string? Reason)
{
    public bool HasGroups => Groups.Count > 0;

    public static TimesheetSummary Unavailable(string reason) => new([], reason);
}

/// <summary>
/// Rewrites a timesheet as the handful of lines a client should read.
///
/// A timesheet is a record of sittings; an invoice is a description of work.
/// Printing one as the other is how an invoice ends up with twenty-eight rows
/// of the same job at varying hours.
///
/// <para>
/// This decides <em>grouping and wording only</em>. It is given hours for
/// context but never returns them: every number on the resulting invoice is
/// summed from the entries by <c>TimesheetGrouping</c>, which also rejects any
/// grouping that does not account for each entry exactly once. A model cannot
/// change what a client owes, only how the work is described.
/// </para>
///
/// <para>
/// Fails soft, like translation. No API key, no allowance left, a provider
/// outage or an unparseable reply all return an empty summary with a reason —
/// never an exception. The caller keeps the unsummarised rows, which are less
/// readable but always correct.
/// </para>
/// </summary>
public interface ITimesheetSummariser
{
    /// <summary>Configured and usable. False means the UI should not offer it.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Proposes how to group <paramref name="entries"/> for a client.
    /// </summary>
    /// <param name="userId">The account being billed for the call.</param>
    /// <param name="clientName">Who the invoice is for, for context.</param>
    /// <param name="tone">How the descriptions should read — see <see cref="Ai.SummaryTone"/>.</param>
    Task<TimesheetSummary> SummariseAsync(
        string userId,
        string clientName,
        IReadOnlyList<TimesheetEntry> entries,
        string tone,
        CancellationToken cancellationToken = default);
}
