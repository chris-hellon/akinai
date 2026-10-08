using Akinai.Domain.Common;
using Akinai.Domain.Enums;
using Akinai.Domain.Exceptions;

namespace Akinai.Domain.Entities;

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

    // --- Settlement analysis -------------------------------------------------
    // Why the amount received differs from the amount invoiced. Recorded when the
    // transaction is matched, because the three causes are unrelated and only one of them
    // means the client still owes money: an FX spread and a bank fee are deductions in
    // transit, and withholding tax is a complete payment whose remainder went to the
    // payer's tax authority. Without this they all render as "short payment" and get
    // chased, which in the withholding case means chasing a client for tax they have
    // already remitted on your behalf.

    /// <summary>The <c>PaymentDifference.Cause</c> this match was classified as.</summary>
    public string? SettlementCause { get; private set; }

    public decimal? WithholdingPercentage { get; private set; }
    public decimal? WithholdingAmount { get; private set; }

    /// <summary>
    /// Whether the payer has supplied the certificate for tax withheld. The outstanding
    /// item on a withheld payment is paperwork, not money, and it is worth real cash at
    /// year end — it is what lets the tax be credited rather than lost.
    /// </summary>
    public bool WithholdingCertificateReceived { get; private set; }

    /// <summary>Realised gain or loss on conversion, in the invoice currency.</summary>
    public decimal? FxGainOrLoss { get; private set; }

    /// <summary>The part no rule accounts for — the only figure it is ever right to chase.</summary>
    public decimal? UnexplainedAmount { get; private set; }

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

    /// <summary>
    /// Records what the classifier made of this payment. Separate from
    /// <see cref="MatchToInvoice"/> so that re-running the analysis never disturbs the
    /// match itself.
    /// </summary>
    public void RecordSettlement(
        string cause,
        decimal? withholdingPercentage,
        decimal? withholdingAmount,
        decimal? fxGainOrLoss,
        decimal? unexplainedAmount)
    {
        SettlementCause = cause;
        WithholdingPercentage = withholdingPercentage;
        WithholdingAmount = withholdingAmount;
        FxGainOrLoss = fxGainOrLoss;
        UnexplainedAmount = unexplainedAmount;
    }

    /// <summary>The payer sent the withholding certificate; nothing further is outstanding.</summary>
    public void MarkWithholdingCertificateReceived()
    {
        if (WithholdingPercentage is null)
            throw new DomainException("No tax was withheld on this payment, so there is no certificate to record");

        WithholdingCertificateReceived = true;
    }

    public void UnmatchInvoice()
    {
        // The analysis described this payment against that invoice. Kept after unmatching
        // it would be stale, and worse, believed.
        SettlementCause = null;
        WithholdingPercentage = null;
        WithholdingAmount = null;
        WithholdingCertificateReceived = false;
        FxGainOrLoss = null;
        UnexplainedAmount = null;

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
