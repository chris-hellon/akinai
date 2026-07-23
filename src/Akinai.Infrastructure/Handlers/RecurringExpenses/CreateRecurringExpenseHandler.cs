using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.Entities;
using Akinai.Domain.Enums;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.RecurringExpenses;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.RecurringExpenses;

public class CreateRecurringExpenseHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService, IEntitlementService entitlementService) : IHandle<CreateRecurringExpense, Guid>
{
    public async Task<HttpResult<Guid>> Handle(CreateRecurringExpense request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var category = Enum.TryParse<ExpenseCategory>(request.Category, out var c) ? c : ExpenseCategory.Other;
        var paymentMethod = Enum.TryParse<PaymentMethod>(request.PaymentMethod, out var p) ? p : PaymentMethod.Other;
        var frequency = Enum.TryParse<RecurrenceFrequency>(request.Frequency, out var f) ? f : RecurrenceFrequency.Month;

        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult<Guid>.Unauthorized();

        // Recurring billing is a paid feature.
        var entitlements = await entitlementService.GetAsync(userId, cancellationToken);
        if (!entitlements.RecurringBilling) return HttpResult<Guid>.Forbidden();

        var recurringExpense = RecurringExpense.Create(
            userId,
            category,
            request.MerchantName,
            paymentMethod,
            request.Amount,
            request.Currency,
            request.FrequencyInterval,
            frequency,
            request.StartDate,
            request.EndDate,
            request.TaxAmount,
            request.IsTaxInclusive,
            request.MerchantTaxNumber,
            request.GroupName,
            request.Notes,
            request.IsReimbursable,
            request.IsBillable,
            request.ClientId,
            request.ProjectId);

        db.RecurringExpenses.Add(recurringExpense);
        await db.SaveChangesAsync(cancellationToken);
        return HttpResult<Guid>.Created(recurringExpense.Id);
    }
}
