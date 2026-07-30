using Akinai.Domain.Common;
using Akinai.Domain.Exceptions;

namespace Akinai.Domain.Entities;

/// <summary>
/// One summarised services row on an invoice — several days of tracked time
/// presented as the single piece of work a client recognises.
///
/// A timesheet and an invoice want opposite things. Tracking wants a row per
/// sitting, because that is how the work happened; a client wants to read what
/// they are paying for. Left alone the two collapse into each other, which is
/// how an invoice ends up with twenty-eight rows of the same job name at
/// varying hours.
///
/// When an invoice has any of these they replace the per-entry rows entirely.
/// The time entries stay linked to the invoice — profitability and utilisation
/// reporting still need them — they simply stop being what the client reads.
///
/// <para>
/// <b>The amount is not the model's opinion.</b> Summarisation decides only how
/// entries are grouped and what the grouping is called. Hours and Amount are
/// summed from the underlying entries by <c>TimesheetGrouping</c>, so the
/// invoice total is arithmetic on data the user already tracked, and cannot be
/// changed by a language model.
/// </para>
/// </summary>
public class InvoiceServiceLine : BaseEntity
{
    public Guid InvoiceId { get; private set; }

    /// <summary>What the client reads. The one field a model contributes.</summary>
    public string Description { get; private set; } = null!;

    /// <summary>Sum of billable hours across the entries in this group.</summary>
    public decimal Hours { get; private set; }

    /// <summary>
    /// Sum of hours × that entry's own rate, per entry.
    ///
    /// Summed per entry rather than Hours × one rate because a group can span
    /// jobs charged differently — summarising across them must not quietly
    /// reprice the work.
    /// </summary>
    public decimal Amount { get; private set; }

    public int Order { get; private set; }

    public Invoice Invoice { get; private set; } = null!;

    /// <summary>
    /// The blended rate, for display only. Equals the real rate when a group
    /// shares one, and is the honest average when it does not.
    /// </summary>
    public decimal EffectiveRate => Hours == 0 ? 0 : Amount / Hours;

    private InvoiceServiceLine() { }

    public static InvoiceServiceLine Create(
        Guid invoiceId,
        string description,
        decimal hours,
        decimal amount,
        int order)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new DomainException("A service line needs a description.");
        if (hours < 0)
            throw new DomainException("A service line cannot have negative hours.");
        if (amount < 0)
            throw new DomainException("A service line cannot have a negative amount.");

        return new InvoiceServiceLine
        {
            InvoiceId = invoiceId,
            Description = description.Trim(),
            Hours = hours,
            Amount = amount,
            Order = order,
        };
    }

    /// <summary>
    /// Edits the wording. Deliberately the only mutator — the user is expected
    /// to reword a summary, never to restate what it cost.
    /// </summary>
    public void Reword(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new DomainException("A service line needs a description.");

        Description = description.Trim();
    }
}
