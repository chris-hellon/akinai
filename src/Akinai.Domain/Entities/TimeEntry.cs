using Akinai.Domain.Common;
using Akinai.Domain.Exceptions;
using Akinai.Domain.ValueObjects;

namespace Akinai.Domain.Entities;

public class TimeEntry : BaseEntity, IAuditableEntity
{
    public Guid JobId { get; private set; }
    public DateOnly Date { get; private set; }
    public Hours Hours { get; private set; } = null!;
    public string? Description { get; private set; }
    public string UserId { get; private set; } = null!;

    // Simple link to invoice - just the FK
    public Guid? InvoiceId { get; private set; }

    /// <summary>
    /// The hourly rate this entry was billed at, captured at the moment it was linked
    /// to an invoice. Null while the entry is still unbilled.
    ///
    /// Frozen deliberately. The live rate cascades job override → project override →
    /// client default, so without this snapshot a later edit at any of those three
    /// levels would silently restate every invoice already sent — the same reason
    /// <see cref="Update"/> refuses to touch a billed entry.
    /// </summary>
    public decimal? BilledHourlyRate { get; private set; }

    public Job Job { get; private set; } = null!;
    public Invoice? Invoice { get; private set; }

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    private TimeEntry() { }

    public static TimeEntry Create(
        Guid jobId,
        DateOnly date,
        decimal hours,
        string? description,
        string userId)
    {
        if (hours <= 0 || hours > 24)
            throw new DomainException("Hours must be between 0 and 24");

        return new TimeEntry
        {
            JobId = jobId,
            Date = date,
            Hours = new Hours(hours),
            Description = description,
            UserId = userId
        };
    }

    public void Update(decimal hours, string? description)
    {
        if (InvoiceId != null)
            throw new DomainException("Cannot modify a billed time entry");

        if (hours <= 0 || hours > 24)
            throw new DomainException("Hours must be between 0 and 24");

        Hours = new Hours(hours);
        Description = description;
    }

    public bool IsBilled => InvoiceId != null;

    /// <summary>
    /// Hours rounded to the nearest quarter-hour (15 min) — the unit time is billed in.
    /// Every invoice-total path (detail view, PDF, email, list, and the stored total /
    /// currency snapshots) must bill off this so they can never disagree. AwayFromZero
    /// so an exact midpoint rounds up rather than using banker's rounding.
    /// </summary>
    public decimal BillableHours => Math.Round(Hours.Value * 4m, MidpointRounding.AwayFromZero) / 4m;

    /// <summary>
    /// The rate this entry bills at: the frozen rate once it is on an invoice, the
    /// job's current rate while it is not. Every path that prices an entry — invoice
    /// view, PDF, email, stored totals, client portal — must read this rather than
    /// calling <c>Job.GetEffectiveHourlyRate()</c> itself, or a rate change will
    /// reprice work that has already been billed.
    /// </summary>
    /// <remarks>Requires <see cref="Job"/> to be loaded for the unbilled case.</remarks>
    public decimal BillingRate => BilledHourlyRate ?? Job.GetEffectiveHourlyRate();

    /// <param name="hourlyRate">
    /// The job's effective rate as of now. Captured so the invoice keeps billing at
    /// this rate however the job, project or client rate changes afterwards.
    /// </param>
    public void LinkToInvoice(Guid invoiceId, decimal hourlyRate)
    {
        if (InvoiceId != null)
            throw new DomainException("Time entry is already linked to an invoice");

        InvoiceId = invoiceId;
        BilledHourlyRate = hourlyRate;
    }

    /// <summary>
    /// Returns the entry to unbilled. The frozen rate is released with it — once the
    /// entry is off the invoice it prices at the job's current rate again, and will
    /// re-freeze if it is billed onto another one.
    /// </summary>
    public void UnlinkFromInvoice()
    {
        InvoiceId = null;
        BilledHourlyRate = null;
    }
}
