using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Domain.Enums;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Shared.Api;
using InvoiceSoftware.Shared.Api.RecurringExpenses;
using InvoiceSoftware.Shared.Dtos.RecurringExpenses;
using InvoiceSoftware.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Infrastructure.Handlers.RecurringExpenses;

public class GetRecurringExpensesHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService) : IHandle<GetRecurringExpenses, PaginatedResponse<RecurringExpenseSummaryDto>>
{
    public async Task<HttpResult<PaginatedResponse<RecurringExpenseSummaryDto>>> Handle(GetRecurringExpenses request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult<PaginatedResponse<RecurringExpenseSummaryDto>>.Unauthorized();

        var query = db.RecurringExpenses
            .Include(re => re.Client)
            .Include(re => re.Project)
            .Where(re => re.UserId == userId);

        if (!string.IsNullOrWhiteSpace(request.Category) && Enum.TryParse<ExpenseCategory>(request.Category, out var category))
            query = query.Where(re => re.Category == category);

        if (request.IsActive.HasValue)
            query = query.Where(re => re.IsActive == request.IsActive.Value);

        if (request.ClientId.HasValue)
            query = query.Where(re => re.ClientId == request.ClientId.Value);

        if (request.ProjectId.HasValue)
            query = query.Where(re => re.ProjectId == request.ProjectId.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(re =>
                re.MerchantName.ToLower().Contains(search) ||
                (re.Notes != null && re.Notes.ToLower().Contains(search)) ||
                (re.GroupName != null && re.GroupName.ToLower().Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var expenses = await query
            .OrderBy(re => re.NextExpenseDate)
            .ThenBy(re => re.MerchantName)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var items = expenses.Select(re => new RecurringExpenseSummaryDto(
            re.Id,
            re.Category.ToString(),
            re.MerchantName,
            re.PaymentMethod.ToString(),
            re.Amount.Amount,
            re.Amount.Currency,
            re.FrequencyInterval,
            re.Frequency.ToString(),
            re.StartDate,
            re.EndDate,
            re.IsActive,
            re.NextExpenseDate,
            re.GeneratedCount,
            re.IsBillable,
            re.ClientId,
            re.Client?.Name,
            re.ProjectId,
            re.Project?.Name)).ToList();

        var result = new PaginatedResponse<RecurringExpenseSummaryDto>
        {
            Items = items,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount
        };

        return HttpResult<PaginatedResponse<RecurringExpenseSummaryDto>>.Ok(result);
    }
}
