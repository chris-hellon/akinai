using System.Text;
using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Admin;
using Akinai.Shared.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;

namespace Akinai.Infrastructure.Handlers.Admin;

/// <summary>Sends the standard password-reset email — the same token flow the
/// user would get from self-service "forgot password".</summary>
public class SendPasswordResetLinkHandler(
    UserManager<ApplicationUser> userManager,
    IEmailSender<ApplicationUser> emailSender,
    IHttpContextAccessor httpContextAccessor,
    ICurrentUserService currentUserService)
    : IHandle<SendPasswordResetLink>
{
    public async Task<HttpResult> Handle(SendPasswordResetLink request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsInRole(AdminRoles.Admin)) return HttpResult.Forbidden();

        var user = await userManager.FindByIdAsync(request.UserId);
        if (user is null || string.IsNullOrEmpty(user.Email)) return HttpResult.NotFound();

        var code = await userManager.GeneratePasswordResetTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

        var req = httpContextAccessor.HttpContext?.Request;
        var baseUrl = req is null ? "" : $"{req.Scheme}://{req.Host}";
        var link = QueryHelpers.AddQueryString($"{baseUrl}/account/reset-password",
            new Dictionary<string, string?> { ["code"] = code });

        await emailSender.SendPasswordResetLinkAsync(user, user.Email, link);
        return HttpResult.NoContent();
    }
}
