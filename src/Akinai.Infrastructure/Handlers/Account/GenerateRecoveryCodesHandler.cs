using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Account;
using Akinai.Shared.Dtos.Account;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Akinai.Infrastructure.Handlers.Account;

public class GenerateRecoveryCodesHandler(
    UserManager<ApplicationUser> userManager,
    IHttpContextAccessor httpContextAccessor,
    ILogger<GenerateRecoveryCodesHandler> logger) : IHandle<GenerateRecoveryCodes, RecoveryCodesDto>
{
    public async Task<HttpResult<RecoveryCodesDto>> Handle(GenerateRecoveryCodes request, CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(httpContextAccessor.HttpContext?.User!);
        if (user is null)
            return HttpResult<RecoveryCodesDto>.Unauthorized();

        var isTwoFactorEnabled = await userManager.GetTwoFactorEnabledAsync(user);
        if (!isTwoFactorEnabled)
            throw new InvalidOperationException("Cannot generate recovery codes for user because they do not have 2FA enabled.");

        var recoveryCodes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        logger.LogInformation("User with ID '{UserId}' has generated new 2FA recovery codes.", user.Id);

        return HttpResult<RecoveryCodesDto>.Ok(new RecoveryCodesDto(recoveryCodes?.ToArray() ?? []));
    }
}
