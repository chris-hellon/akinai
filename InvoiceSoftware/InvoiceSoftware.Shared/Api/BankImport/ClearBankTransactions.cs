using BlazorUtils.EasyApi;

namespace InvoiceSoftware.Shared.Api.BankImport;

/// <summary>Bulk-delete the current user's transactions, optionally limited by date range / status.</summary>
[Route("api/bank-import/transactions/clear")]
public class ClearBankTransactions : IPost<int>
{
    [BodyParam]
    public DateOnly? FromDate { get; init; }

    [BodyParam]
    public DateOnly? ToDate { get; init; }

    /// <summary>null/empty = all; otherwise "unmatched" | "matched" | "expense" | "ignored".</summary>
    [BodyParam]
    public string? Status { get; init; }

    /// <summary>"in" | "out" | null.</summary>
    [BodyParam]
    public string? Direction { get; init; }

    [BodyParam]
    public string? Currency { get; init; }
}
