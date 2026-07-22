using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Shared.Api.Account;
using InvoiceSoftware.Shared.Dtos.Account;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace InvoiceSoftware.Infrastructure.Handlers.Account;

public class GetTwoFactorStatusHandler(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IHttpContextAccessor httpContextAccessor) : IHandle<GetTwoFactorStatus, TwoFactorStatusDto>
{
    public async Task<HttpResult<TwoFactorStatusDto>> Handle(GetTwoFactorStatus request, CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(httpContextAccessor.HttpContext?.User!);
        if (user is null)
            return HttpResult<TwoFactorStatusDto>.Unauthorized();

        var is2faEnabled = await userManager.GetTwoFactorEnabledAsync(user);
        // IsTwoFactorClientRememberedAsync authenticates the "remember this
        // machine" cookie, which tries to write to the response. During an
        // interactive Blazor render the response has already started, so it
        // throws "OnStarting cannot be set because the response has already
        // started" and took the whole page down. The flag is display-only.
        var isMachineRemembered = false;
        try
        {
            isMachineRemembered = await signInManager.IsTwoFactorClientRememberedAsync(user);
        }
        catch (InvalidOperationException)
        {
            // Response already started — leave it false.
        }
        var recoveryCodesLeft = await userManager.CountRecoveryCodesAsync(user);
        var hasAuthenticator = await userManager.GetAuthenticatorKeyAsync(user) is not null;

        return HttpResult<TwoFactorStatusDto>.Ok(new TwoFactorStatusDto(
            is2faEnabled,
            isMachineRemembered,
            recoveryCodesLeft,
            hasAuthenticator));
    }
}
