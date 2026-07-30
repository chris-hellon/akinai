using Akinai.Domain.Entities;

namespace Akinai.Domain.Services;

/// <summary>
/// Turns a proposed grouping of time entries into invoice service lines.
///
/// This is the boundary between what a language model is allowed to decide and
/// what it is not. A model proposes <em>which entries belong together</em> and
/// <em>what to call the group</em>. Everything with a number in it happens
/// here, from the entries themselves.
///
/// Kept in the domain rather than beside the AI client on purpose: it is the
/// rule that makes summarisation safe, it is worth testing without a network
/// call, and it must apply identically if a grouping ever comes from somewhere
/// other than a model — a saved preset, a manual regroup, a different provider.
/// </summary>
public static class TimesheetGrouping
{
    /// <summary>A proposed group: the entries in it, and what to call it.</summary>
    public readonly record struct ProposedGroup(string Description, IReadOnlyList<Guid> TimeEntryIds);

    public sealed record Result(
        bool Accepted,
        IReadOnlyList<InvoiceServiceLine> Lines,
        string? Rejection)
    {
        public static Result Reject(string reason) => new(false, [], reason);
    }

    /// <summary>
    /// Validates a proposed grouping and builds the service lines for it.
    ///
    /// Rejects — rather than repairs — a grouping that does not account for
    /// every entry exactly once. A repaired grouping would silently drop or
    /// double-bill work, and a wrong invoice is far worse than an ugly one:
    /// the caller falls back to unsummarised rows, which are at least correct.
    /// </summary>
    /// <param name="entries">Every billable entry on the invoice, with its own rate.</param>
    public static Result Build(
        Guid invoiceId,
        IReadOnlyCollection<BillableEntry> entries,
        IReadOnlyList<ProposedGroup> groups)
    {
        if (groups.Count == 0)
            return Result.Reject("The grouping was empty.");

        var byId = entries.ToDictionary(e => e.TimeEntryId);
        var seen = new HashSet<Guid>();

        foreach (var group in groups)
        {
            if (string.IsNullOrWhiteSpace(group.Description))
                return Result.Reject("A group arrived without a description.");

            if (group.TimeEntryIds.Count == 0)
                return Result.Reject($"Group \"{group.Description}\" contained no entries.");

            foreach (var id in group.TimeEntryIds)
            {
                if (!byId.ContainsKey(id))
                    return Result.Reject("The grouping referenced an entry that is not on this invoice.");

                // A duplicate would bill the same hours twice.
                if (!seen.Add(id))
                    return Result.Reject("The grouping used the same entry in more than one group.");
            }
        }

        // And the other direction: anything left out would be unbilled work.
        if (seen.Count != byId.Count)
            return Result.Reject($"The grouping covered {seen.Count} of {byId.Count} entries.");

        var lines = new List<InvoiceServiceLine>(groups.Count);
        var order = 0;

        foreach (var group in groups)
        {
            var members = group.TimeEntryIds.Select(id => byId[id]).ToList();

            var hours = members.Sum(m => m.BillableHours);
            // Per entry, at that entry's own rate — see InvoiceServiceLine.Amount.
            var amount = members.Sum(m => m.BillableHours * m.HourlyRate);

            lines.Add(InvoiceServiceLine.Create(
                invoiceId,
                group.Description,
                hours,
                amount,
                order++));
        }

        return new Result(true, lines, null);
    }

    /// <summary>
    /// One billable time entry, reduced to what grouping needs: identity, and
    /// the two numbers that decide what it is worth.
    /// </summary>
    public readonly record struct BillableEntry(
        Guid TimeEntryId,
        decimal BillableHours,
        decimal HourlyRate);
}
