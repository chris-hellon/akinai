using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Account;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace Akinai.Infrastructure.Handlers.Account;

public class UpdateProfileHandler(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IHttpContextAccessor httpContextAccessor) : IHandle<UpdateProfile>
{
    public async Task<HttpResult> Handle(UpdateProfile request, CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(httpContextAccessor.HttpContext?.User!);
        if (user is null)
            return HttpResult.Unauthorized();

        var currentPhoneNumber = await userManager.GetPhoneNumberAsync(user);
        if (request.PhoneNumber != currentPhoneNumber)
        {
            var result = await userManager.SetPhoneNumberAsync(user, request.PhoneNumber);
            if (!result.Succeeded)
                throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        await signInManager.RefreshSignInAsync(user);
        return HttpResult.NoContent();
    }
}
