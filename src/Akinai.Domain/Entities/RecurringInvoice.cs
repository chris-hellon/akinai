using Akinai.Domain.Common;
using Akinai.Domain.Enums;
using Akinai.Domain.Exceptions;

namespace Akinai.Domain.Entities;

public class RecurringInvoice : AggregateRoot, IAuditableEntity
{
    public string UserId { get; private set; } = null!;
    public Guid ClientId { get; private set; }
    public string TemplateName { get; private set; } = null!;
    public string? Notes { get; private set; }
    public string? Terms { get; private set; }
    public string? Footer { get; private set; }
    public decimal TaxRate { get; private set; }
    public string Currency { get; private set; } = "USD";

    public int FrequencyInterval { get; private set; } = 1;
    public RecurrenceFrequency Frequency { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public int DueDays { get; private set; } = 30;
    public bool IsActive { get; private set; } = true;

    public DateOnly? LastGeneratedDate { get; private set; }
    public DateOnly NextInvoiceDate { get; private set; }
    public int GeneratedCount { get; private set; }

    public Client Client { get; private set; } = null!;

    private readonly List<RecurringInvoiceLineItem> _lineItems = [];
    public IReadOnlyCollection<RecurringInvoiceLineItem> LineItems => _lineItems.AsReadOnly();

    private readonly List<Invoice> _generatedInvoices = [];
    public IReadOnlyCollection<Invoice> GeneratedInvoices => _generatedInvoices.AsReadOnly();

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    private RecurringInvoice() { }

    public static RecurringInvoice Create(
        string userId,
        Guid clientId,
        string templateName,
        string currency,
        int frequencyInterval,
        RecurrenceFrequency frequency,
        DateOnly startDate,
        int dueDays = 30,
        DateOnly? endDate = null,
        decimal taxRate = 0,
        string? notes = null,
        string? terms = null,
        string? footer = null)
    {
        if (string.IsNullOrWhiteSpace(templateName))
            throw new DomainException("Template name is required");

        return new RecurringInvoice
        {
            UserId = userId,
            ClientId = clientId,
            TemplateName = templateName,
            Currency = currency,
            FrequencyInterval = Math.Max(1, frequencyInterval),
            Frequency = frequency,
            StartDate = startDate,
            EndDate = endDate,
            NextInvoiceDate = startDate,
            DueDays = dueDays,
            TaxRate = taxRate,
            Notes = notes,
            Terms = terms,
            Footer = footer
        };
    }

    public void Update(
        string templateName,
        int frequencyInterval,
        RecurrenceFrequency frequency,
        DateOnly startDate,
        DateOnly? endDate,
        int dueDays,
        decimal taxRate,
        string? notes,
        string? terms,
        string? footer)
    {
        if (string.IsNullOrWhiteSpace(templateName))
            throw new DomainException("Template name is required");

        TemplateName = templateName;
        FrequencyInterval = Math.Max(1, frequencyInterval);
        Frequency = frequency;
        StartDate = startDate;
        EndDate = endDate;
        DueDays = dueDays;
        TaxRate = taxRate;
        Notes = notes;
        Terms = terms;
        Footer = footer;

        if (LastGeneratedDate == null)
            NextInvoiceDate = startDate;
    }

    public RecurringInvoiceLineItem AddLineItem(
        string description,
        decimal quantity,
        decimal unitPrice,
        Guid? productId = null)
    {
        var order = _lineItems.Count;
        var item = RecurringInvoiceLineItem.Create(Id, description, quantity, unitPrice, Currency, order, productId);
        _lineItems.Add(item);
        return item;
    }

    public void RemoveLineItem(Guid lineItemId)
    {
        var item = _lineItems.FirstOrDefault(x => x.Id == lineItemId);
        if (item != null)
        {
            _lineItems.Remove(item);
            ReorderLineItems();
        }
    }

    public void ClearLineItems()
    {
        _lineItems.Clear();
    }

    private void ReorderLineItems()
    {
        for (var i = 0; i < _lineItems.Count; i++)
            _lineItems[i].SetOrder(i);
    }

    public Invoice? GenerateInvoice(string invoiceNumber, DateOnly asOfDate)
    {
        if (!IsActive) return null;
        if (NextInvoiceDate > asOfDate) return null;
        if (!_lineItems.Any()) return null;

        if (EndDate.HasValue && NextInvoiceDate > EndDate.Value)
        {
            IsActive = false;
            return null;
        }

        var invoice = Invoice.Create(
            invoiceNumber,
            ClientId,
            NextInvoiceDate,
            NextInvoiceDate.AddDays(DueDays),
            TaxRate,
            Notes,
            Currency);

        _generatedInvoices.Add(invoice);
        LastGeneratedDate = NextInvoiceDate;
        GeneratedCount++;
        CalculateNextInvoiceDate();

        if (EndDate.HasValue && NextInvoiceDate > EndDate.Value)
        {
            IsActive = false;
        }

        return invoice;
    }

    private void CalculateNextInvoiceDate()
    {
        NextInvoiceDate = Frequency switch
        {
            RecurrenceFrequency.Day => NextInvoiceDate.AddDays(FrequencyInterval),
            RecurrenceFrequency.Week => NextInvoiceDate.AddDays(FrequencyInterval * 7),
            RecurrenceFrequency.Month => NextInvoiceDate.AddMonths(FrequencyInterval),
            RecurrenceFrequency.Year => NextInvoiceDate.AddYears(FrequencyInterval),
            _ => NextInvoiceDate.AddMonths(FrequencyInterval)
        };
    }

    public void Pause() => IsActive = false;

    public void Resume()
    {
        if (EndDate.HasValue && DateOnly.FromDateTime(DateTime.UtcNow) > EndDate.Value)
            throw new DomainException("Cannot resume a recurring invoice that has passed its end date");
        IsActive = true;
    }

    public string GetFrequencyDescription()
    {
        var intervalText = FrequencyInterval == 1 ? "" : $"{FrequencyInterval} ";
        var frequencyText = Frequency switch
        {
            RecurrenceFrequency.Day => FrequencyInterval == 1 ? "day" : "days",
            RecurrenceFrequency.Week => FrequencyInterval == 1 ? "week" : "weeks",
            RecurrenceFrequency.Month => FrequencyInterval == 1 ? "month" : "months",
            RecurrenceFrequency.Year => FrequencyInterval == 1 ? "year" : "years",
            _ => "month"
        };
        return $"Every {intervalText}{frequencyText}";
    }
}
