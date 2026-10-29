using Akinai.Domain.Common;
using Akinai.Domain.Enums;
using Akinai.Domain.Exceptions;

namespace Akinai.Domain.Entities;

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
    // --- Delivery record ----------------------------------------------------
    // What was sent, to whom, and when. Kept because a cross-border dispute is
    // usually about the document rather than the amount, and because the client
    // benefits from it as much as the sender: a stored hash proves the invoice was
    // not altered after the fact by either party.

    /// <summary>When the invoice was emailed, if it has been.</summary>
    public DateTime? SentAtUtc { get; private set; }

    /// <summary>The address it was sent to.</summary>
    public string? SentToEmail { get; private set; }

    /// <summary>SHA-256 of the PDF that was attached, as lower-case hex.</summary>
    public string? SentDocumentSha256 { get; private set; }

    /// <summary>
    /// When an email send was begun, cleared the moment it finishes either way.
    ///
    /// Exists so that an in-flight send is a state the database can name. Sending commits
    /// the status before it renders and emails the document, so a process that dies in
    /// between leaves an invoice marked Sent that nobody ever received. That was said to
    /// be "wrong but detectable" — it was not. <see cref="MarkSentManually"/> produces
    /// exactly the same row, and marking an invoice sent by hand is a normal thing to do,
    /// so "Sent with no delivery record" describes the ordinary case far more often than
    /// the broken one. Anything sweeping on that signature would have returned invoices
    /// to draft that the user had deliberately marked as sent.
    ///
    /// Set only by <see cref="BeginSend"/>, so it is null on every invoice that predates
    /// it and on every manual mark. A row where this is set and <see cref="SentAtUtc"/> is
    /// not is unambiguously a send that started and never finished.
    /// </summary>
    public DateTime? SendStartedAtUtc { get; private set; }

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

    // Summarised services rows. When any exist they are what the client reads
    // instead of one row per time entry; the entries above stay linked either way.
    private readonly List<InvoiceServiceLine> _serviceLines = [];
    public IReadOnlyCollection<InvoiceServiceLine> ServiceLines => _serviceLines.AsReadOnly();

    /// <summary>True when the services section is a summary rather than raw entries.</summary>
    public bool HasSummarisedServices => _serviceLines.Count > 0;

    // The total converted into each currency the owner reports in, snapshotted at save.
    private readonly List<InvoiceCurrencyTotal> _currencyTotals = [];
    public IReadOnlyCollection<InvoiceCurrencyTotal> CurrencyTotals => _currencyTotals.AsReadOnly();

    // Currencies this invoice accepts payment in besides its own, chosen per invoice.
    private readonly List<InvoicePaymentCurrency> _paymentCurrencies = [];
    public IReadOnlyCollection<InvoicePaymentCurrency> PaymentCurrencies => _paymentCurrencies.AsReadOnly();

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

    /// <summary>
    /// Leaves draft. Shared by both routes out of it; callers use <see cref="BeginSend"/>
    /// or <see cref="MarkSentManually"/> rather than this, so that which route was taken
    /// is recorded rather than lost.
    /// </summary>
    private void LeaveDraft()
    {
        if (Status != InvoiceStatus.Draft)
            throw new DomainException("Only draft invoices can be sent");

        Status = InvoiceStatus.Sent;

        // Generate public access token if not already set
        PublicAccessToken ??= Guid.NewGuid();
    }

    /// <summary>
    /// Starts an email send: the status is committed now, and the document is rendered
    /// and emailed afterwards. <see cref="SendStartedAtUtc"/> marks it as in flight so
    /// that an attempt which never reaches <see cref="RecordDelivery"/> or
    /// <see cref="AbandonSend"/> — because the process died — can be told apart from an
    /// invoice someone simply marked as sent.
    /// </summary>
    public void BeginSend(DateTime startedAtUtc)
    {
        LeaveDraft();
        SendStartedAtUtc = startedAtUtc;
    }

    /// <summary>
    /// Records that the user sent this invoice themselves — by their own mail client, by
    /// post, by hand. A finished state, not an in-flight one: there is no delivery record
    /// to wait for, because Akinai did not do the sending and cannot attest to it.
    /// </summary>
    public void MarkSentManually()
    {
        LeaveDraft();
    }

    /// <summary>
    /// Undoes a send that never reached anyone, returning the invoice to draft.
    ///
    /// Sending is two-phase: the status is committed before the document is rendered, so
    /// that the PDF printed from the invoice's own page shows "Sent" rather than "Draft",
    /// and this puts it back if the render or the email then fails.
    ///
    /// It is <em>not</em> a general un-send, and the guard is what makes that true: once
    /// <see cref="SentAtUtc"/> is stamped the email has gone, the client holds the
    /// document, and this throws. The invariant that matters — an invoice someone has
    /// actually been sent cannot be walked back — is preserved exactly.
    ///
    /// A process that dies between the two phases leaves Status=Sent with no delivery
    /// record. That state is wrong but detectable, and it is the safe direction: it
    /// over-reports rather than silently losing a send.
    /// </summary>
    public void AbandonSend()
    {
        if (Status != InvoiceStatus.Sent)
            throw new DomainException("Only an in-flight send can be abandoned");

        if (SentAtUtc is not null)
            throw new DomainException("This invoice has already been delivered and cannot be returned to draft");

        Status = InvoiceStatus.Draft;
        SendStartedAtUtc = null;
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
    /// Replaces the summarised services rows wholesale.
    ///
    /// Replace rather than append: a summary is one view of the same set of
    /// time entries, so merging two of them would double-count the work.
    /// </summary>
    public void ReplaceServiceLines(IEnumerable<InvoiceServiceLine> lines)
    {
        if (Status != InvoiceStatus.Draft)
            throw new DomainException("Cannot modify a non-draft invoice");

        _serviceLines.Clear();
        _serviceLines.AddRange(lines);
    }

    /// <summary>
    /// Drops the summary, so the services section returns to one row per time
    /// entry. Always available — a summary must never be a one-way door.
    /// </summary>
    public void ClearServiceLines()
    {
        if (Status != InvoiceStatus.Draft)
            throw new DomainException("Cannot modify a non-draft invoice");

        _serviceLines.Clear();
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

    /// <summary>
    /// Records what actually went out. Called after the email succeeds, so a failed send
    /// leaves no delivery record claiming otherwise.
    /// </summary>
    public void RecordDelivery(DateTime sentAtUtc, string? toEmail, string? documentSha256)
    {
        SentAtUtc = sentAtUtc;
        SentToEmail = toEmail;
        SentDocumentSha256 = documentSha256;

        // The send is over, so it is no longer in flight. Left set, the invoice would be
        // swept as abandoned despite having been delivered — and AbandonSend's own guard
        // is the only thing that would then stop it being walked back.
        SendStartedAtUtc = null;
    }
}
