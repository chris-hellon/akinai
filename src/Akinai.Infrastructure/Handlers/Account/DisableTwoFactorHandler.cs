using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Account;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Akinai.Infrastructure.Handlers.Account;

public class DisableTwoFactorHandler(
    UserManager<ApplicationUser> userManager,
    IHttpContextAccessor httpContextAccessor,
    ILogger<DisableTwoFactorHandler> logger) : IHandle<DisableTwoFactor>
{
    public async Task<HttpResult> Handle(DisableTwoFactor request, CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(httpContextAccessor.HttpContext?.User!);
        if (user is null)
            return HttpResult.Unauthorized();

        var disable2faResult = await userManager.SetTwoFactorEnabledAsync(user, false);
        if (!disable2faResult.Succeeded)
            throw new InvalidOperationException("Unexpected error occurred disabling 2FA.");

        logger.LogInformation("User with ID '{UserId}' has disabled 2FA.", user.Id);

        return HttpResult.NoContent();
    }
}
