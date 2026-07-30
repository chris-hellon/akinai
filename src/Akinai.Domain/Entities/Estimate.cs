using Akinai.Domain.Common;
using Akinai.Domain.Enums;
using Akinai.Domain.Exceptions;
using Akinai.Domain.ValueObjects;

namespace Akinai.Domain.Entities;

public class Estimate : AggregateRoot, IAuditableEntity
{
    public string UserId { get; private set; } = null!;
    public string EstimateNumber { get; private set; } = null!;
    public Guid ClientId { get; private set; }
    public DateOnly EstimateDate { get; private set; }
    public DateOnly? ExpiryDate { get; private set; }
    public int ValidDays { get; private set; } = 30;
    public EstimateStatus Status { get; private set; } = EstimateStatus.Draft;
    public decimal TaxRate { get; private set; }
    public string Currency { get; private set; } = "USD";
    public string? Notes { get; private set; }
    public string? Terms { get; private set; }
    public Guid? PublicAccessToken { get; private set; }
    public Guid? ConvertedInvoiceId { get; private set; }
    public Guid? TemplateId { get; private set; }
    public DateOnly? AcceptedDate { get; private set; }
    public DateOnly? RejectedDate { get; private set; }

    /// <summary>
    /// The language this estimate is written in.
    ///
    /// Null means "not decided here" — fall back to the client's override, then
    /// the account default. Once set it pins, exactly as it does on an invoice:
    /// an estimate a client has accepted is the basis of what they agreed to,
    /// and changing an account default later must not restate it.
    /// </summary>
    public SupportedLanguage? Language { get; private set; }

    public Client Client { get; private set; } = null!;
    public Invoice? ConvertedInvoice { get; private set; }
    public InvoiceTemplate? Template { get; private set; }

    private readonly List<EstimateLineItem> _lineItems = [];
    public IReadOnlyCollection<EstimateLineItem> LineItems => _lineItems.AsReadOnly();

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    private Estimate() { }

    public static Estimate Create(
        string userId,
        string estimateNumber,
        Guid clientId,
        DateOnly estimateDate,
        string currency,
        int validDays = 30,
        decimal taxRate = 0,
        string? notes = null,
        string? terms = null,
        SupportedLanguage? language = null)
    {
        return new Estimate
        {
            Language = language,
            UserId = userId,
            EstimateNumber = estimateNumber,
            ClientId = clientId,
            EstimateDate = estimateDate,
            ValidDays = validDays,
            ExpiryDate = estimateDate.AddDays(validDays),
            TaxRate = taxRate,
            Currency = currency,
            Notes = notes,
            Terms = terms,
            PublicAccessToken = Guid.NewGuid()
        };
    }

    public void UpdateDetails(
        DateOnly estimateDate,
        int validDays,
        decimal taxRate,
        string? notes,
        string? terms,
        SupportedLanguage? language = null)
    {
        if (Status != EstimateStatus.Draft)
            throw new DomainException("Cannot modify a non-draft estimate");

        Language = language;

        EstimateDate = estimateDate;
        ValidDays = validDays;
        ExpiryDate = estimateDate.AddDays(validDays);
        TaxRate = taxRate;
        Notes = notes;
        Terms = terms;
    }

    public void SetTemplate(Guid? templateId)
    {
        TemplateId = templateId;
    }

    public EstimateLineItem AddLineItem(
        string description,
        decimal quantity,
        decimal unitPrice,
        Guid? productId = null)
    {
        if (Status != EstimateStatus.Draft)
            throw new DomainException("Cannot add items to a non-draft estimate");

        var order = _lineItems.Count;
        var item = EstimateLineItem.Create(Id, description, quantity, unitPrice, Currency, order, productId);
        _lineItems.Add(item);
        return item;
    }

    public void RemoveLineItem(Guid lineItemId)
    {
        if (Status != EstimateStatus.Draft)
            throw new DomainException("Cannot remove items from a non-draft estimate");

        var item = _lineItems.FirstOrDefault(x => x.Id == lineItemId);
        if (item != null)
        {
            _lineItems.Remove(item);
            ReorderLineItems();
        }
    }

    public void ClearLineItems()
    {
        if (Status != EstimateStatus.Draft)
            throw new DomainException("Cannot clear items from a non-draft estimate");

        _lineItems.Clear();
    }

    private void ReorderLineItems()
    {
        for (var i = 0; i < _lineItems.Count; i++)
            _lineItems[i].SetOrder(i);
    }

    public Money CalculateSubtotal()
    {
        var total = _lineItems.Sum(item => item.LineTotal.Amount);
        return new Money(total, Currency);
    }

    public Money CalculateTax()
    {
        var subtotal = CalculateSubtotal();
        return subtotal.Multiply(TaxRate / 100);
    }

    public Money CalculateTotal()
    {
        return CalculateSubtotal().Add(CalculateTax());
    }

    public void Send()
    {
        if (Status != EstimateStatus.Draft)
            throw new DomainException("Only draft estimates can be sent");
        if (!_lineItems.Any())
            throw new DomainException("Cannot send an estimate with no line items");

        Status = EstimateStatus.Sent;
        PublicAccessToken ??= Guid.NewGuid();
    }

    public void Accept()
    {
        if (Status != EstimateStatus.Sent)
            throw new DomainException("Only sent estimates can be accepted");

        Status = EstimateStatus.Accepted;
        AcceptedDate = DateOnly.FromDateTime(DateTime.UtcNow);
    }

    public void Reject()
    {
        if (Status != EstimateStatus.Sent)
            throw new DomainException("Only sent estimates can be rejected");

        Status = EstimateStatus.Rejected;
        RejectedDate = DateOnly.FromDateTime(DateTime.UtcNow);
    }

    public void MarkAsExpired()
    {
        if (Status == EstimateStatus.Sent && ExpiryDate.HasValue &&
            DateOnly.FromDateTime(DateTime.UtcNow) > ExpiryDate.Value)
        {
            Status = EstimateStatus.Expired;
        }
    }

    public void MarkAsConverted(Guid invoiceId)
    {
        if (Status != EstimateStatus.Accepted)
            throw new DomainException("Only accepted estimates can be converted to invoices");

        Status = EstimateStatus.Converted;
        ConvertedInvoiceId = invoiceId;
    }

    public bool IsExpired => ExpiryDate.HasValue &&
        DateOnly.FromDateTime(DateTime.UtcNow) > ExpiryDate.Value &&
        Status == EstimateStatus.Sent;
}
