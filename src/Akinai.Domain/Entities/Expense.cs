using Akinai.Domain.Common;
using Akinai.Domain.Enums;
using Akinai.Domain.Exceptions;
using Akinai.Domain.ValueObjects;

namespace Akinai.Domain.Entities;

public class Expense : AggregateRoot, IAuditableEntity
{
    /// <summary>Owning user. An expense need not belong to a client or project,
    /// so ownership cannot be derived — it has to be stored here.</summary>
    public string UserId { get; private set; } = null!;

    public ExpenseCategory Category { get; private set; }
    public string MerchantName { get; private set; } = null!;
    public DateOnly ExpenseDate { get; private set; }
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
    public bool IsBilled { get; private set; }
    public Guid? InvoiceId { get; private set; }

    public Guid? RecurringExpenseId { get; private set; }

    public Client? Client { get; private set; }
    public Project? Project { get; private set; }
    public Invoice? Invoice { get; private set; }
    public RecurringExpense? RecurringExpense { get; private set; }

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    private Expense() { }

    public static Expense Create(
        string userId,
        ExpenseCategory category,
        string merchantName,
        DateOnly expenseDate,
        PaymentMethod paymentMethod,
        decimal amount,
        string currency = "USD",
        decimal taxAmount = 0,
        bool isTaxInclusive = false,
        string? merchantTaxNumber = null,
        string? groupName = null,
        string? notes = null,
        bool isReimbursable = false,
        bool isBillable = false,
        Guid? clientId = null,
        Guid? projectId = null,
        Guid? recurringExpenseId = null)
    {
        return new Expense
        {
            UserId = userId,
            Category = category,
            MerchantName = merchantName,
            ExpenseDate = expenseDate,
            PaymentMethod = paymentMethod,
            Amount = new Money(amount, currency),
            TaxAmount = Math.Round(taxAmount, 2),
            IsTaxInclusive = isTaxInclusive,
            MerchantTaxNumber = merchantTaxNumber,
            GroupName = groupName,
            Notes = notes,
            IsReimbursable = isReimbursable,
            IsBillable = isBillable,
            ClientId = isBillable ? clientId : null,
            ProjectId = isBillable ? projectId : null,
            RecurringExpenseId = recurringExpenseId
        };
    }

    public void Update(
        ExpenseCategory category,
        string merchantName,
        DateOnly expenseDate,
        PaymentMethod paymentMethod,
        decimal amount,
        string currency,
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
        if (IsBilled)
            throw new DomainException("Cannot modify a billed expense");

        Category = category;
        MerchantName = merchantName;
        ExpenseDate = expenseDate;
        PaymentMethod = paymentMethod;
        Amount = new Money(amount, currency);
        TaxAmount = Math.Round(taxAmount, 2);
        IsTaxInclusive = isTaxInclusive;
        MerchantTaxNumber = merchantTaxNumber;
        GroupName = groupName;
        Notes = notes;
        IsReimbursable = isReimbursable;
        IsBillable = isBillable;
        ClientId = isBillable ? clientId : null;
        ProjectId = isBillable ? projectId : null;
    }

    public void MarkAsBilled(Guid invoiceId)
    {
        if (!IsBillable)
            throw new DomainException("Expense is not billable");
        if (IsBilled)
            throw new DomainException("Expense is already billed");

        IsBilled = true;
        InvoiceId = invoiceId;
    }

    public void UnmarkBilled()
    {
        IsBilled = false;
        InvoiceId = null;
    }

    public decimal GetTotalAmount()
    {
        return IsTaxInclusive ? Amount.Amount : Amount.Amount + TaxAmount;
    }

    public decimal GetNetAmount()
    {
        return IsTaxInclusive ? Amount.Amount - TaxAmount : Amount.Amount;
    }
}
