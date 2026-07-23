using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Account;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Akinai.Infrastructure.Handlers.Account;

public class DeleteAccountHandler(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IHttpContextAccessor httpContextAccessor,
    ILogger<DeleteAccountHandler> logger) : IHandle<DeleteAccount>
{
    public async Task<HttpResult> Handle(DeleteAccount request, CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(httpContextAccessor.HttpContext?.User!);
        if (user is null)
            return HttpResult.Unauthorized();

        var hasPassword = await userManager.HasPasswordAsync(user);
        if (hasPassword)
        {
            var passwordValid = await userManager.CheckPasswordAsync(user, request.Password);
            if (!passwordValid)
                throw new InvalidOperationException("Incorrect password.");
        }

        var result = await userManager.DeleteAsync(user);
        if (!result.Succeeded)
            throw new InvalidOperationException("Unexpected error occurred deleting user.");

        await signInManager.SignOutAsync();
        logger.LogInformation("User with ID '{UserId}' deleted themselves.", user.Id);

        return HttpResult.NoContent();
    }
}
