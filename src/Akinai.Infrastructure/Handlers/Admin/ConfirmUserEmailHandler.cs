using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Admin;
using Akinai.Shared.Services;
using Microsoft.AspNetCore.Identity;

namespace Akinai.Infrastructure.Handlers.Admin;

/// <summary>
/// Confirms an email on the user's behalf, for when the confirmation mail never
/// arrived. Uses a real generated token rather than writing EmailConfirmed
/// directly, so Identity's own bookkeeping (security stamp) stays consistent.
/// </summary>
public class ConfirmUserEmailHandler(
    UserManager<ApplicationUser> userManager,
    ICurrentUserService currentUserService)
    : IHandle<ConfirmUserEmail>
{
    public async Task<HttpResult> Handle(ConfirmUserEmail request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsInRole(AdminRoles.Admin))
            return HttpResult.Forbidden();

        var user = await userManager.FindByIdAsync(request.UserId);
        if (user is null) return HttpResult.NotFound();
        if (user.EmailConfirmed) return HttpResult.NoContent();

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var result = await userManager.ConfirmEmailAsync(user, token);

        return result.Succeeded ? HttpResult.NoContent() : HttpResult.BadRequest();
    }
}
