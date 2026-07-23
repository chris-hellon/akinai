using Akinai.Domain.Common;
using Akinai.Domain.Enums;
using Akinai.Domain.Exceptions;
using Akinai.Domain.ValueObjects;

namespace Akinai.Domain.Entities;

public class RecurringExpense : AggregateRoot, IAuditableEntity
{
    /// <summary>Owning user — see Expense.UserId.</summary>
    public string UserId { get; private set; } = null!;

    public ExpenseCategory Category { get; private set; }
    public string MerchantName { get; private set; } = null!;
    public PaymentMethod PaymentMethod { get; private set; }
    public Money Amount { get; private set; } = null!;

    public decimal TaxAmount { get; private set; }
    public bool IsTaxInclusive { get; private set; }
    public string? MerchantTaxNumber { get; private set; }

    public string? GroupName { get; private set; }
    public string? Notes { get; private set; }

    public bool IsReimbursable { get; private set; }
    public bool IsBillable { get; private set; }
    public Guid? ClientId { get; private set; }
    public Guid? ProjectId { get; private set; }

    public int FrequencyInterval { get; private set; } = 1;
    public RecurrenceFrequency Frequency { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public bool IsActive { get; private set; } = true;

    public DateOnly? LastGeneratedDate { get; private set; }
    public DateOnly NextExpenseDate { get; private set; }
    public int GeneratedCount { get; private set; }

    public Client? Client { get; private set; }
    public Project? Project { get; private set; }

    private readonly List<Expense> _generatedExpenses = [];
    public IReadOnlyCollection<Expense> GeneratedExpenses => _generatedExpenses.AsReadOnly();

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    private RecurringExpense() { }

    public static RecurringExpense Create(
        string userId,
        ExpenseCategory category,
        string merchantName,
        PaymentMethod paymentMethod,
        decimal amount,
        string currency,
        int frequencyInterval,
        RecurrenceFrequency frequency,
        DateOnly startDate,
        DateOnly? endDate = null,
        decimal taxAmount = 0,
        bool isTaxInclusive = false,
        string? merchantTaxNumber = null,
        string? groupName = null,
        string? notes = null,
        bool isReimbursable = false,
        bool isBillable = false,
        Guid? clientId = null,
        Guid? projectId = null)
    {
        return new RecurringExpense
        {
            UserId = userId,
            Category = category,
            MerchantName = merchantName,
            PaymentMethod = paymentMethod,
            Amount = new Money(amount, currency),
            FrequencyInterval = Math.Max(1, frequencyInterval),
            Frequency = frequency,
            StartDate = startDate,
            EndDate = endDate,
            NextExpenseDate = startDate,
            TaxAmount = Math.Round(taxAmount, 2),
            IsTaxInclusive = isTaxInclusive,
            MerchantTaxNumber = merchantTaxNumber,
            GroupName = groupName,
            Notes = notes,
            IsReimbursable = isReimbursable,
            IsBillable = isBillable,
            ClientId = isBillable ? clientId : null,
            ProjectId = isBillable ? projectId : null
        };
    }

    public void Update(
        ExpenseCategory category,
        string merchantName,
        PaymentMethod paymentMethod,
        decimal amount,
        string currency,
        int frequencyInterval,
        RecurrenceFrequency frequency,
        DateOnly startDate,
        DateOnly? endDate,
        decimal taxAmount,
        bool isTaxInclusive,
        string? merchantTaxNumber,
        string? groupName,
        string? notes,
        bool isReimbursable,
        bool isBillable,
        Guid? clientId,
        Guid? projectId)
    {
        Category = category;
        MerchantName = merchantName;
        PaymentMethod = paymentMethod;
        Amount = new Money(amount, currency);
        FrequencyInterval = Math.Max(1, frequencyInterval);
        Frequency = frequency;
        StartDate = startDate;
        EndDate = endDate;
        TaxAmount = Math.Round(taxAmount, 2);
        IsTaxInclusive = isTaxInclusive;
        MerchantTaxNumber = merchantTaxNumber;
        GroupName = groupName;
        Notes = notes;
        IsReimbursable = isReimbursable;
        IsBillable = isBillable;
        ClientId = isBillable ? clientId : null;
        ProjectId = isBillable ? projectId : null;

        if (LastGeneratedDate == null)
            NextExpenseDate = startDate;
    }

    public Expense? GenerateExpense(DateOnly asOfDate)
    {
        if (!IsActive) return null;
        if (NextExpenseDate > asOfDate) return null;
        if (EndDate.HasValue && NextExpenseDate > EndDate.Value)
        {
            IsActive = false;
            return null;
        }

        var expense = Expense.Create(
            UserId,   // generated expenses inherit the schedule's owner
            Category,
            MerchantName,
            NextExpenseDate,
            PaymentMethod,
            Amount.Amount,
            Amount.Currency,
            TaxAmount,
            IsTaxInclusive,
            MerchantTaxNumber,
            GroupName,
            Notes,
            IsReimbursable,
            IsBillable,
            ClientId,
            ProjectId,
            Id);

        _generatedExpenses.Add(expense);
        LastGeneratedDate = NextExpenseDate;
        GeneratedCount++;
        CalculateNextExpenseDate();

        if (EndDate.HasValue && NextExpenseDate > EndDate.Value)
        {
            IsActive = false;
        }

        return expense;
    }

    private void CalculateNextExpenseDate()
    {
        NextExpenseDate = Frequency switch
        {
            RecurrenceFrequency.Day => NextExpenseDate.AddDays(FrequencyInterval),
            RecurrenceFrequency.Week => NextExpenseDate.AddDays(FrequencyInterval * 7),
            RecurrenceFrequency.Month => NextExpenseDate.AddMonths(FrequencyInterval),
            RecurrenceFrequency.Year => NextExpenseDate.AddYears(FrequencyInterval),
            _ => NextExpenseDate.AddMonths(FrequencyInterval)
        };
    }

    public void Pause() => IsActive = false;

    public void Resume()
    {
        if (EndDate.HasValue && DateOnly.FromDateTime(DateTime.UtcNow) > EndDate.Value)
            throw new DomainException("Cannot resume a recurring expense that has passed its end date");
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
