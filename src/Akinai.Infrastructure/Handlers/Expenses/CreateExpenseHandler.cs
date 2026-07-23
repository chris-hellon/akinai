using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.Entities;
using Akinai.Domain.Enums;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Expenses;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Expenses;

public class CreateExpenseHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService) : IHandle<CreateExpense, Guid>
{
    public async Task<HttpResult<Guid>> Handle(CreateExpense request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var category = Enum.TryParse<ExpenseCategory>(request.Category, out var c) ? c : ExpenseCategory.Other;
        var paymentMethod = Enum.TryParse<PaymentMethod>(request.PaymentMethod, out var p) ? p : PaymentMethod.Other;

        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult<Guid>.Unauthorized();

        var expense = Expense.Create(
            userId,
            category,
            request.MerchantName,
            request.ExpenseDate,
            paymentMethod,
            request.Amount,
            request.Currency,
            request.TaxAmount,
            request.IsTaxInclusive,
            request.MerchantTaxNumber,
            request.GroupName,
            request.Notes,
            request.IsReimbursable,
            request.IsBillable,
            request.ClientId,
            request.ProjectId);

        db.Expenses.Add(expense);
        await db.SaveChangesAsync(cancellationToken);
        return HttpResult<Guid>.Created(expense.Id);
    }
}
