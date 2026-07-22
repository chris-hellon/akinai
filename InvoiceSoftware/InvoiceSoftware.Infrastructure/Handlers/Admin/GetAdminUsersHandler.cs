using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Shared.Api;
using InvoiceSoftware.Shared.Api.Admin;
using InvoiceSoftware.Shared.Dtos.Admin;
using InvoiceSoftware.Shared.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Infrastructure.Handlers.Admin;

/// <summary>
/// Lists every registered account. This is the one place in the app that is
/// *meant* to read across tenants, which is why the role is re-checked here
/// and not left to the middleware alone.
/// </summary>
public class GetAdminUsersHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    UserManager<ApplicationUser> userManager,
    ICurrentUserService currentUserService)
    : IHandle<GetAdminUsers, PaginatedResponse<AdminUserDto>>
{
    public async Task<HttpResult<PaginatedResponse<AdminUserDto>>> Handle(
        GetAdminUsers request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsInRole(AdminRoles.Admin))
            return HttpResult<PaginatedResponse<AdminUserDto>>.Forbidden();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var query = db.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(u => u.Email!.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var users = await query
            .OrderBy(u => u.Email)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var ids = users.Select(u => u.Id).ToList();

        // Counts in two grouped queries rather than per-user round trips.
        var clientCounts = await db.Clients
            .Where(c => ids.Contains(c.UserId))
            .GroupBy(c => c.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, cancellationToken);

        var invoiceCounts = await db.Invoices
            .Where(i => ids.Contains(i.Client.UserId))
            .GroupBy(i => i.Client.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, cancellationToken);

        var adminIds = (await userManager.GetUsersInRoleAsync(AdminRoles.Admin))
            .Select(u => u.Id).ToHashSet();

        var now = DateTimeOffset.UtcNow;
        var callerId = currentUserService.UserId;
        var items = users.Select(u => new AdminUserDto(
            u.Id,
            u.Email ?? "",
            u.EmailConfirmed,
            u.TwoFactorEnabled,
            adminIds.Contains(u.Id),
            u.LockoutEnd is not null && u.LockoutEnd > now,
            u.LockoutEnd,
            clientCounts.GetValueOrDefault(u.Id),
            invoiceCounts.GetValueOrDefault(u.Id),
            u.Id == callerId)).ToList();

        return HttpResult<PaginatedResponse<AdminUserDto>>.Ok(new PaginatedResponse<AdminUserDto>
        {
            Items = items,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount
        });
    }
}
