using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.Entities;
using Akinai.Domain.Enums;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.BankImport;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.BankImport;

public class CreateExpenseFromTransactionHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService)
    : IHandle<CreateExpenseFromTransaction, Guid>
{
    public async Task<HttpResult<Guid>> Handle(CreateExpenseFromTransaction request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<Guid>.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var transaction = await db.BankTransactions
            .FirstOrDefaultAsync(bt => bt.Id == request.Id && bt.UserId == userId, cancellationToken);

        if (transaction is null)
            return HttpResult<Guid>.NotFound();

        // Already handled — guarded by the UI, so this is just defence in depth.
        if (transaction.IsExpensed || transaction.IsMatched || transaction.IsIgnored)
            return HttpResult<Guid>.BadRequest(Guid.Empty);

        var category = Enum.TryParse<ExpenseCategory>(request.Category, out var c) ? c : ExpenseCategory.Other;

        var expense = Expense.Create(
            userId,
            category,
            merchantName: transaction.Description,
            expenseDate: transaction.TransactionDate,
            paymentMethod: PaymentMethod.BankTransfer,
            amount: Math.Abs(transaction.Amount),
            currency: transaction.Currency,
            notes: string.IsNullOrWhiteSpace(request.Notes)
                ? $"Imported from bank statement ({transaction.SourceFileName})"
                : request.Notes,
            isBillable: request.IsBillable,
            clientId: request.ClientId,
            projectId: request.ProjectId);

        db.Expenses.Add(expense);
        await db.SaveChangesAsync(cancellationToken);

        transaction.LinkToExpense(expense.Id);
        await db.SaveChangesAsync(cancellationToken);

        return HttpResult<Guid>.Created(expense.Id);
    }
}
