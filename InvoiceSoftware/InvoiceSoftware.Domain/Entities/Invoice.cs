using InvoiceSoftware.Domain.Common;
using InvoiceSoftware.Domain.Enums;
using InvoiceSoftware.Domain.Exceptions;

namespace InvoiceSoftware.Domain.Entities;

public class Invoice : AggregateRoot, IAuditableEntity
{
    public string InvoiceNumber { get; private set; } = null!;
    public Guid ClientId { get; private set; }
    public DateOnly IssueDate { get; private set; }
    public DateOnly DueDate { get; private set; }
    public InvoiceStatus Status { get; private set; } = InvoiceStatus.Draft;
    public decimal TaxRate { get; private set; }
    public string? Notes { get; private set; }
    public DateOnly? PaidDate { get; private set; }
    public string Currency { get; private set; } = "USD";
    public Guid? PublicAccessToken { get; private set; }
    public Guid? TemplateId { get; private set; }

    /// <summary>
    /// The language this invoice is written in.
    ///
    /// Null means "not decided here" — fall back to the client's override, then
    /// the account default. Once set, it pins: changing your account default
    /// later must not silently rewrite an invoice a client has already been
    /// sent, any more than it should change the amount.
    /// </summary>
    public SupportedLanguage? Language { get; private set; }

    /// <summary>
    /// When the client first opened the invoice link, and when they last did.
    ///
    /// Denormalised onto the invoice rather than kept as a view log: the only
    /// questions anyone asks are "have they seen it?" and "when?", and both the
    /// list and the detail page would otherwise need a join for a badge.
    /// A per-view history can be added later without changing these.
    /// </summary>
    public DateTime? FirstViewedAtUtc { get; private set; }
    public DateTime? LastViewedAtUtc { get; private set; }
    public int ViewCount { get; private set; }

    /// <summary>
    /// Records that the client opened the invoice.
    ///
    /// Callers must decide whether a request counts — the owner previewing
    /// their own link, and the PDF renderer loading the same page, both reach
    /// the invoice the same way and neither is the client reading it. A view
    /// counter that counts your own PDF generation is worse than none.
    /// </summary>
    public void RecordView(DateTime nowUtc)
    {
        FirstViewedAtUtc ??= nowUtc;
        LastViewedAtUtc = nowUtc;
        ViewCount++;
    }

    // Totals snapshotted at save time (in the invoice's own currency) so historical
    // amounts don't move when a client's rate or the FX rate later changes.
    public decimal Subtotal { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal Total { get; private set; }

    public Client Client { get; private set; } = null!;
    public InvoiceTemplate? Template { get; private set; }

    // Simple link to time entries - just IDs, no complex entity management
    private readonly List<TimeEntry> _timeEntries = [];
    public IReadOnlyCollection<TimeEntry> TimeEntries => _timeEntries.AsReadOnly();

    // Product line items for direct product billing
    private readonly List<InvoiceLineItem> _lineItems = [];
    public IReadOnlyCollection<InvoiceLineItem> LineItems => _lineItems.AsReadOnly();

    // The total converted into each currency the owner reports in, snapshotted at save.
    private readonly List<InvoiceCurrencyTotal> _currencyTotals = [];
    public IReadOnlyCollection<InvoiceCurrencyTotal> CurrencyTotals => _currencyTotals.AsReadOnly();

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    private Invoice() { }

    public static Invoice Create(
        string invoiceNumber,
        Guid clientId,
        DateOnly issueDate,
        DateOnly dueDate,
        decimal taxRate = 0,
        string? notes = null,
        string currency = "USD",
        SupportedLanguage? language = null)
    {
        return new Invoice
        {
            InvoiceNumber = invoiceNumber,
            ClientId = clientId,
            IssueDate = issueDate,
            DueDate = dueDate,
            TaxRate = taxRate,
            Notes = notes,
            Currency = currency,
            Language = language,
            PublicAccessToken = Guid.NewGuid()
        };
    }

    public void UpdateDetails(
        DateOnly issueDate,
        DateOnly dueDate,
        decimal taxRate,
        string? notes,
        Guid? templateId = null,
        SupportedLanguage? language = null)
    {
        if (Status != InvoiceStatus.Draft)
            throw new DomainException("Cannot modify a non-draft invoice");

        IssueDate = issueDate;
        DueDate = dueDate;
        TaxRate = taxRate;
        Notes = notes;
        TemplateId = templateId;
        Language = language;
    }

    public void SetTemplate(Guid? templateId)
    {
        TemplateId = templateId;
    }

    public void Send()
    {
        if (Status != InvoiceStatus.Draft)
            throw new DomainException("Only draft invoices can be sent");

        Status = InvoiceStatus.Sent;

        // Generate public access token if not already set
        PublicAccessToken ??= Guid.NewGuid();
    }

    public void MarkAsPaid(DateOnly paidDate)
    {
        if (Status == InvoiceStatus.Paid)
            throw new DomainException("Invoice is already marked as paid");
        if (Status == InvoiceStatus.Void)
            throw new DomainException("Cannot mark a voided invoice as paid");

        Status = InvoiceStatus.Paid;
        PaidDate = paidDate;
    }

    public void MarkAsOverdue()
    {
        if (Status == InvoiceStatus.Sent)
            Status = InvoiceStatus.Overdue;
    }

    public void Void()
    {
        if (Status == InvoiceStatus.Paid)
            throw new DomainException("Cannot void a paid invoice");

        Status = InvoiceStatus.Void;
    }

    public void AddLineItem(InvoiceLineItem lineItem)
    {
        if (Status != InvoiceStatus.Draft)
            throw new DomainException("Cannot modify a non-draft invoice");
        _lineItems.Add(lineItem);
    }

    public void RemoveLineItem(Guid lineItemId)
    {
        if (Status != InvoiceStatus.Draft)
            throw new DomainException("Cannot modify a non-draft invoice");
        var item = _lineItems.FirstOrDefault(li => li.Id == lineItemId);
        if (item != null)
            _lineItems.Remove(item);
    }

    public void ClearLineItems()
    {
        if (Status != InvoiceStatus.Draft)
            throw new DomainException("Cannot modify a non-draft invoice");
        _lineItems.Clear();
    }

    /// <summary>
    /// Freezes the invoice's monetary totals. <paramref name="currencyTotals"/> maps each
    /// reporting currency to the total converted into it at save time.
    /// </summary>
    /// <summary>
    /// Sets the snapshotted scalar totals. The per-currency snapshot rows
    /// (<see cref="CurrencyTotals"/>) are persisted separately by the recalculation
    /// service via the DbSet, so EF tracks them as inserts rather than updates.
    /// </summary>
    public void SetTotals(decimal subtotal, decimal taxAmount, decimal total)
    {
        Subtotal = Math.Round(subtotal, 2);
        TaxAmount = Math.Round(taxAmount, 2);
        Total = Math.Round(total, 2);
    }
}
