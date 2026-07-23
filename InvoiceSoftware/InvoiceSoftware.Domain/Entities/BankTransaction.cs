using InvoiceSoftware.Domain.Common;
using InvoiceSoftware.Domain.Enums;
using InvoiceSoftware.Domain.Exceptions;

namespace InvoiceSoftware.Domain.Entities;

public class BankTransaction : AggregateRoot, IAuditableEntity
{
    public string UserId { get; private set; } = null!;
    public DateOnly TransactionDate { get; private set; }
    public string Description { get; private set; } = null!;
    /// <summary>Signed transaction amount — positive for money in, negative for money out.</summary>
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = null!;
    public string? Reference { get; private set; }
    public string? BankAccountName { get; private set; }

    public Guid? MatchedInvoiceId { get; private set; }
    public MatchConfidence MatchConfidence { get; private set; } = MatchConfidence.None;
    public string? MatchNotes { get; private set; }

    public DateTime ImportedAt { get; private set; }
    public string SourceFileName { get; private set; } = null!;
    public int SourceRowNumber { get; private set; }

    /// <summary>Stable provider transaction id when pulled from a bank feed (null for CSV imports).</summary>
    public string? ExternalId { get; private set; }

    public bool IsMatched => MatchedInvoiceId.HasValue;
    public bool IsIgnored { get; private set; }
    public string? IgnoreReason { get; private set; }

    public Guid? ExpenseId { get; private set; }
    public bool IsExpensed => ExpenseId.HasValue;

    public Invoice? MatchedInvoice { get; private set; }

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    private BankTransaction() { }

    public static BankTransaction Create(
        string userId,
        DateOnly transactionDate,
        string description,
        decimal amount,
        string currency,
        string sourceFileName,
        int sourceRowNumber,
        string? reference = null,
        string? bankAccountName = null)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new DomainException("Transaction description is required");

        return new BankTransaction
        {
            UserId = userId,
            TransactionDate = transactionDate,
            Description = description,
            Amount = Math.Round(amount, 2),
            Currency = currency,
            Reference = reference,
            BankAccountName = bankAccountName,
            SourceFileName = sourceFileName,
            SourceRowNumber = sourceRowNumber,
            ImportedAt = DateTime.UtcNow
        };
    }

    /// <summary>Create a transaction pulled from a live bank feed, keyed by the provider's stable id.</summary>
    public static BankTransaction CreateFromFeed(
        string userId,
        string externalId,
        DateOnly transactionDate,
        string description,
        decimal amount,
        string currency,
        string? reference,
        string source)
    {
        return new BankTransaction
        {
            UserId = userId,
            ExternalId = externalId,
            TransactionDate = transactionDate,
            Description = string.IsNullOrWhiteSpace(description) ? "(no description)" : description.Trim(),
            Amount = Math.Round(amount, 2),
            Currency = currency,
            Reference = reference,
            SourceFileName = source,
            SourceRowNumber = 0,
            ImportedAt = DateTime.UtcNow
        };
    }

    public void MatchToInvoice(Guid invoiceId, MatchConfidence confidence, string? notes = null)
    {
        if (IsIgnored)
            throw new DomainException("Cannot match an ignored transaction");
        if (IsMatched)
            throw new DomainException("Transaction is already matched to an invoice");

        MatchedInvoiceId = invoiceId;
        MatchConfidence = confidence;
        MatchNotes = notes;
    }

    public void UnmatchInvoice()
    {
        MatchedInvoiceId = null;
        MatchConfidence = MatchConfidence.None;
        MatchNotes = null;
    }

    public void LinkToExpense(Guid expenseId)
    {
        if (IsIgnored)
            throw new DomainException("Cannot create an expense from an ignored transaction");
        if (IsMatched)
            throw new DomainException("Transaction is already matched to an invoice");
        if (IsExpensed)
            throw new DomainException("Transaction has already been recorded as an expense");

        ExpenseId = expenseId;
    }

    public void UnlinkExpense() => ExpenseId = null;

    public void Ignore(string? reason = null)
    {
        if (IsMatched)
            throw new DomainException("Cannot ignore a matched transaction");
        if (IsExpensed)
            throw new DomainException("Cannot ignore a transaction recorded as an expense");

        IsIgnored = true;
        IgnoreReason = reason;
    }

    public void Unignore()
    {
        IsIgnored = false;
        IgnoreReason = null;
    }
}
