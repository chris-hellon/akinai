using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Domain.Enums;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Shared.Api.RecurringExpenses;
using InvoiceSoftware.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Infrastructure.Handlers.RecurringExpenses;

public class UpdateRecurringExpenseHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService) : IHandle<UpdateRecurringExpense>
{
    public async Task<HttpResult> Handle(UpdateRecurringExpense request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult.Unauthorized();

        var recurringExpense = await db.RecurringExpenses.FirstOrDefaultAsync(re => re.Id == request.Id && re.UserId == userId, cancellationToken);
        if (recurringExpense is null) return HttpResult.NotFound();

        var category = Enum.TryParse<ExpenseCategory>(request.Category, out var c) ? c : ExpenseCategory.Other;
        var paymentMethod = Enum.TryParse<PaymentMethod>(request.PaymentMethod, out var p) ? p : PaymentMethod.Other;
        var frequency = Enum.TryParse<RecurrenceFrequency>(request.Frequency, out var f) ? f : RecurrenceFrequency.Month;

        recurringExpense.Update(
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

        await db.SaveChangesAsync(cancellationToken);
        return HttpResult.NoContent();
    }
}
