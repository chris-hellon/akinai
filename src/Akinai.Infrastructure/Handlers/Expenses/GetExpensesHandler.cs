using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.Enums;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api;
using Akinai.Shared.Api.Expenses;
using Akinai.Shared.Dtos.Expenses;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Expenses;

public class GetExpensesHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService) : IHandle<GetExpenses, PaginatedResponse<ExpenseSummaryDto>>
{
    public async Task<HttpResult<PaginatedResponse<ExpenseSummaryDto>>> Handle(GetExpenses request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult<PaginatedResponse<ExpenseSummaryDto>>.Unauthorized();

        var query = db.Expenses
            .Include(e => e.Client)
            .Include(e => e.Project)
            .Where(e => e.UserId == userId);

        if (!string.IsNullOrWhiteSpace(request.Category) && Enum.TryParse<ExpenseCategory>(request.Category, out var category))
            query = query.Where(e => e.Category == category);

        if (request.FromDate.HasValue)
            query = query.Where(e => e.ExpenseDate >= request.FromDate.Value);

        if (request.ToDate.HasValue)
            query = query.Where(e => e.ExpenseDate <= request.ToDate.Value);

        if (request.IsBillable.HasValue)
            query = query.Where(e => e.IsBillable == request.IsBillable.Value);

        if (request.IsBilled.HasValue)
            query = query.Where(e => e.IsBilled == request.IsBilled.Value);

        if (request.ClientId.HasValue)
            query = query.Where(e => e.ClientId == request.ClientId.Value);

        if (request.ProjectId.HasValue)
            query = query.Where(e => e.ProjectId == request.ProjectId.Value);

        if (!string.IsNullOrWhiteSpace(request.GroupName))
            query = query.Where(e => e.GroupName == request.GroupName);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(e =>
                e.MerchantName.ToLower().Contains(search) ||
                (e.Notes != null && e.Notes.ToLower().Contains(search)) ||
                (e.GroupName != null && e.GroupName.ToLower().Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var expenses = await query
            .OrderByDescending(e => e.ExpenseDate)
            .ThenBy(e => e.MerchantName)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var items = expenses.Select(e => new ExpenseSummaryDto(
            e.Id,
            e.Category.ToString(),
            e.MerchantName,
            e.ExpenseDate,
            e.PaymentMethod.ToString(),
            e.Amount.Amount,
            e.Amount.Currency,
            e.TaxAmount,
            e.IsBillable,
            e.IsBilled,
            e.ClientId,
            e.Client?.Name,
            e.ProjectId,
            e.Project?.Name,
            e.GroupName,
            e.RecurringExpenseId)).ToList();

        var result = new PaginatedResponse<ExpenseSummaryDto>
        {
            Items = items,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount
        };

        return HttpResult<PaginatedResponse<ExpenseSummaryDto>>.Ok(result);
    }
}
