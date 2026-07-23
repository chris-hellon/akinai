using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Account;
using Akinai.Shared.Dtos.Account;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Akinai.Infrastructure.Handlers.Account;

public class EnableTwoFactorHandler(
    UserManager<ApplicationUser> userManager,
    IHttpContextAccessor httpContextAccessor,
    ILogger<EnableTwoFactorHandler> logger) : IHandle<EnableTwoFactor, EnableTwoFactorResultDto>
{
    public async Task<HttpResult<EnableTwoFactorResultDto>> Handle(EnableTwoFactor request, CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(httpContextAccessor.HttpContext?.User!);
        if (user is null)
            return HttpResult<EnableTwoFactorResultDto>.Unauthorized();

        var verificationCode = request.Code.Replace(" ", string.Empty).Replace("-", string.Empty);
        var is2faTokenValid = await userManager.VerifyTwoFactorTokenAsync(
            user, userManager.Options.Tokens.AuthenticatorTokenProvider, verificationCode);

        if (!is2faTokenValid)
        {
            return HttpResult<EnableTwoFactorResultDto>.Ok(
                new EnableTwoFactorResultDto(false, "Verification code is invalid.", null));
        }

        await userManager.SetTwoFactorEnabledAsync(user, true);
        logger.LogInformation("User with ID '{UserId}' has enabled 2FA with an authenticator app.", user.Id);

        var recoveryCodes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);

        return HttpResult<EnableTwoFactorResultDto>.Ok(
            new EnableTwoFactorResultDto(true, null, recoveryCodes?.ToArray()));
    }
}
