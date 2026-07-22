using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Domain.Enums;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Shared.Api.Admin;
using InvoiceSoftware.Shared.Dtos.Admin;
using InvoiceSoftware.Shared.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Infrastructure.Handlers.Admin;

public class GetAdminUserHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    UserManager<ApplicationUser> userManager,
    ICurrentUserService currentUserService)
    : IHandle<GetAdminUser, AdminUserDetailDto?>
{
    public async Task<HttpResult<AdminUserDetailDto?>> Handle(
        GetAdminUser request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsInRole(AdminRoles.Admin))
            return HttpResult<AdminUserDetailDto?>.Forbidden();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);
        if (user is null) return HttpResult<AdminUserDetailDto?>.NotFound();

        var clientCount  = await db.Clients.CountAsync(c => c.UserId == user.Id, cancellationToken);
        var projectCount = await db.Projects.CountAsync(p => p.Client.UserId == user.Id, cancellationToken);
        var timeCount    = await db.TimeEntries.CountAsync(t => t.UserId == user.Id, cancellationToken);

        var invoices = await db.Invoices
            .Where(i => i.Client.UserId == user.Id)
            .Select(i => new { i.Status, Amount = i.Total, i.Currency, i.IssueDate })
            .ToListAsync(cancellationToken);

        var totalInvoiced = invoices.Sum(i => i.Amount);
        var totalPaid     = invoices.Where(i => i.Status == InvoiceStatus.Paid).Sum(i => i.Amount);

        // Mixed currencies can't be meaningfully summed — report the currency
        // only when the account uses exactly one.
        var currencies = invoices.Select(i => i.Currency).Distinct().ToList();
        var currency = currencies.Count == 1 ? currencies[0] : null;

        var isAdmin = await userManager.IsInRoleAsync(user, AdminRoles.Admin);
        var now = DateTimeOffset.UtcNow;

        return HttpResult<AdminUserDetailDto?>.Ok(new AdminUserDetailDto(
            user.Id,
            user.Email ?? "",
            user.EmailConfirmed,
            user.TwoFactorEnabled,
            isAdmin,
            user.LockoutEnd is not null && user.LockoutEnd > now,
            user.LockoutEnd,
            clientCount,
            projectCount,
            invoices.Count,
            timeCount,
            totalInvoiced,
            totalPaid,
            currency,
            invoices.Count == 0 ? null : invoices.Min(i => i.IssueDate).ToDateTime(TimeOnly.MinValue),
            invoices.Count == 0 ? null : invoices.Max(i => i.IssueDate).ToDateTime(TimeOnly.MinValue)));
    }
}
