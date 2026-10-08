using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api;
using Akinai.Shared.Api.BankImport;
using Akinai.Shared.Dtos.BankImport;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.BankImport;

public class GetBankTransactionsHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService)
    : IHandle<GetBankTransactions, PaginatedResponse<BankTransactionDto>>
{
    public async Task<HttpResult<PaginatedResponse<BankTransactionDto>>> Handle(
        GetBankTransactions request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<PaginatedResponse<BankTransactionDto>>.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Self-heal any links to expenses that no longer exist (e.g. the expense was
        // deleted). ExpenseId is a bare column with no FK, so clear the dangling link
        // and the transaction returns to "Needs review".
        var orphaned = await db.BankTransactions
            .Where(bt => bt.UserId == userId && bt.ExpenseId != null
                && !db.Expenses.Any(e => e.Id == bt.ExpenseId))
            .ToListAsync(cancellationToken);
        if (orphaned.Count > 0)
        {
            foreach (var bt in orphaned)
                bt.UnlinkExpense();
            await db.SaveChangesAsync(cancellationToken);
        }

        var query = db.BankTransactions
            .Include(bt => bt.MatchedInvoice)
            .Where(bt => bt.UserId == userId);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(bt => bt.Description.Contains(term)
                || (bt.Reference != null && bt.Reference.Contains(term)));
        }

        // Filter by status
        if (request.IsMatched.HasValue)
            query = query.Where(bt => (bt.MatchedInvoiceId != null) == request.IsMatched.Value);

        if (request.IsIgnored.HasValue)
            query = query.Where(bt => bt.IsIgnored == request.IsIgnored.Value);

        if (request.IsExpensed.HasValue)
            query = query.Where(bt => (bt.ExpenseId != null) == request.IsExpensed.Value);

        if (request.FromDate.HasValue)
            query = query.Where(bt => bt.TransactionDate >= request.FromDate.Value);
        if (request.ToDate.HasValue)
            query = query.Where(bt => bt.TransactionDate <= request.ToDate.Value);

        if (request.Direction == "in")
            query = query.Where(bt => bt.Amount > 0);
        else if (request.Direction == "out")
            query = query.Where(bt => bt.Amount < 0);

        if (!string.IsNullOrWhiteSpace(request.Currency))
            query = query.Where(bt => bt.Currency == request.Currency);

        var totalCount = await query.CountAsync(cancellationToken);

        var transactions = await query
            .OrderByDescending(bt => bt.TransactionDate)
            .ThenByDescending(bt => bt.ImportedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var items = transactions.Select(bt => new BankTransactionDto(
            bt.Id,
            bt.TransactionDate,
            bt.Description,
            bt.Amount,
            bt.Currency,
            bt.Reference,
            bt.BankAccountName,
            bt.MatchedInvoiceId,
            bt.MatchedInvoice?.InvoiceNumber,
            bt.MatchConfidence.ToString(),
            bt.MatchNotes,
            bt.IsMatched,
            bt.IsIgnored,
            bt.IgnoreReason,
            bt.ImportedAt,
            bt.SourceFileName,
            bt.ExpenseId,
            bt.IsExpensed,
            bt.SettlementCause,
            bt.WithholdingPercentage,
            bt.WithholdingAmount,
            bt.WithholdingCertificateReceived,
            bt.FxGainOrLoss,
            bt.UnexplainedAmount)).ToList();

        return HttpResult<PaginatedResponse<BankTransactionDto>>.Ok(
            new PaginatedResponse<BankTransactionDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = request.Page,
                PageSize = request.PageSize
            });
    }
}
