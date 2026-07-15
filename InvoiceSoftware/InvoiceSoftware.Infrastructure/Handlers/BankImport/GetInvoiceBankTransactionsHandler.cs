using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Shared.Api.BankImport;
using InvoiceSoftware.Shared.Dtos.BankImport;
using InvoiceSoftware.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Infrastructure.Handlers.BankImport;

public class GetInvoiceBankTransactionsHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService)
    : IHandle<GetInvoiceBankTransactions, List<BankTransactionDto>>
{
    public async Task<HttpResult<List<BankTransactionDto>>> Handle(
        GetInvoiceBankTransactions request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<List<BankTransactionDto>>.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var transactions = await db.BankTransactions
            .Where(bt => bt.UserId == userId && bt.MatchedInvoiceId == request.InvoiceId)
            .OrderBy(bt => bt.TransactionDate)
            .ToListAsync(cancellationToken);

        var items = transactions.Select(bt => new BankTransactionDto(
            bt.Id, bt.TransactionDate, bt.Description, bt.Amount, bt.Currency,
            bt.Reference, bt.BankAccountName, bt.MatchedInvoiceId, null,
            bt.MatchConfidence.ToString(), bt.MatchNotes, bt.IsMatched, bt.IsIgnored,
            bt.IgnoreReason, bt.ImportedAt, bt.SourceFileName, bt.ExpenseId, bt.IsExpensed)).ToList();

        return HttpResult<List<BankTransactionDto>>.Ok(items);
    }
}
