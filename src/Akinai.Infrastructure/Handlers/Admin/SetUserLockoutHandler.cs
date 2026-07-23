using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Admin;
using Akinai.Shared.Services;
using Microsoft.AspNetCore.Identity;

namespace Akinai.Infrastructure.Handlers.Admin;

public class SetUserLockoutHandler(
    UserManager<ApplicationUser> userManager,
    ICurrentUserService currentUserService)
    : IHandle<SetUserLockout>
{
    public async Task<HttpResult> Handle(SetUserLockout request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsInRole(AdminRoles.Admin))
            return HttpResult.Forbidden();

        // Locking yourself out would be irreversible from the UI.
        if (request.UserId == currentUserService.UserId)
            return HttpResult.BadRequest();

        var user = await userManager.FindByIdAsync(request.UserId);
        if (user is null) return HttpResult.NotFound();

        // Lockout only takes effect when it is enabled for the account.
        await userManager.SetLockoutEnabledAsync(user, true);
        var result = await userManager.SetLockoutEndDateAsync(
            user, request.LockedOut ? DateTimeOffset.MaxValue : null);

        return result.Succeeded ? HttpResult.NoContent() : HttpResult.BadRequest();
    }
}
