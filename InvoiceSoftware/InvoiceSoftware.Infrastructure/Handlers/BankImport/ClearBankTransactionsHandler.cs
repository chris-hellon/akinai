using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Shared.Api.BankImport;
using InvoiceSoftware.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Infrastructure.Handlers.BankImport;

public class ClearBankTransactionsHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService)
    : IHandle<ClearBankTransactions, int>
{
    public async Task<HttpResult<int>> Handle(ClearBankTransactions request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<int>.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.BankTransactions.Where(bt => bt.UserId == userId);

        if (request.FromDate.HasValue)
            query = query.Where(bt => bt.TransactionDate >= request.FromDate.Value);
        if (request.ToDate.HasValue)
            query = query.Where(bt => bt.TransactionDate <= request.ToDate.Value);

        query = request.Status switch
        {
            "matched" => query.Where(bt => bt.MatchedInvoiceId != null),
            "expense" => query.Where(bt => bt.ExpenseId != null),
            "ignored" => query.Where(bt => bt.IsIgnored),
            "unmatched" => query.Where(bt => bt.MatchedInvoiceId == null && bt.ExpenseId == null && !bt.IsIgnored),
            _ => query
        };

        if (request.Direction == "in")
            query = query.Where(bt => bt.Amount > 0);
        else if (request.Direction == "out")
            query = query.Where(bt => bt.Amount < 0);

        if (!string.IsNullOrWhiteSpace(request.Currency))
            query = query.Where(bt => bt.Currency == request.Currency);

        var deleted = await query.ExecuteDeleteAsync(cancellationToken);
        return HttpResult<int>.Ok(deleted);
    }
}
