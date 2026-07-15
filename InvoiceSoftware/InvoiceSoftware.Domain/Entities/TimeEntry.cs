using InvoiceSoftware.Domain.Common;
using InvoiceSoftware.Domain.Exceptions;
using InvoiceSoftware.Domain.ValueObjects;

namespace InvoiceSoftware.Domain.Entities;

public class TimeEntry : BaseEntity, IAuditableEntity
{
    public Guid JobId { get; private set; }
    public DateOnly Date { get; private set; }
    public Hours Hours { get; private set; } = null!;
    public string? Description { get; private set; }
    public string UserId { get; private set; } = null!;

    // Simple link to invoice - just the FK
    public Guid? InvoiceId { get; private set; }

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

    public void LinkToInvoice(Guid invoiceId)
    {
        if (InvoiceId != null)
            throw new DomainException("Time entry is already linked to an invoice");

        InvoiceId = invoiceId;
    }

    public void UnlinkFromInvoice()
    {
        InvoiceId = null;
    }
}
