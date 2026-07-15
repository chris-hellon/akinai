using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Shared.Api.CashFlow;
using InvoiceSoftware.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Infrastructure.Handlers.CashFlow;

public class UpdateCashBalanceHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService)
    : IHandle<UpdateCashBalance>
{
    public async Task<HttpResult> Handle(UpdateCashBalance request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var profile = await db.BusinessProfiles.FirstOrDefaultAsync(bp => bp.UserId == userId, cancellationToken);
        if (profile is null)
            return HttpResult.NotFound();

        profile.SetCashBalance(request.Balance, DateOnly.FromDateTime(DateTime.UtcNow));
        await db.SaveChangesAsync(cancellationToken);
        return HttpResult.Ok();
    }
}
