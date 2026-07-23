using System.Text;
using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Shared.Api.Admin;
using InvoiceSoftware.Shared.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;

namespace InvoiceSoftware.Infrastructure.Handlers.Admin;

/// <summary>
/// Re-sends the confirmation email. Unlike the force-confirm action, the user
/// still has to prove they control the address — preferable when you can.
/// </summary>
public class ResendConfirmationEmailHandler(
    UserManager<ApplicationUser> userManager,
    IEmailSender<ApplicationUser> emailSender,
    IHttpContextAccessor httpContextAccessor,
    ICurrentUserService currentUserService)
    : IHandle<ResendConfirmationEmail>
{
    public async Task<HttpResult> Handle(ResendConfirmationEmail request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsInRole(AdminRoles.Admin)) return HttpResult.Forbidden();

        var user = await userManager.FindByIdAsync(request.UserId);
        if (user is null || string.IsNullOrEmpty(user.Email)) return HttpResult.NotFound();
        if (user.EmailConfirmed) return HttpResult.NoContent();

        var code = await userManager.GenerateEmailConfirmationTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

        var req = httpContextAccessor.HttpContext?.Request;
        var baseUrl = req is null ? "" : $"{req.Scheme}://{req.Host}";
        var link = QueryHelpers.AddQueryString($"{baseUrl}/account/confirm-email",
            new Dictionary<string, string?> { ["userId"] = user.Id, ["code"] = code });

        await emailSender.SendConfirmationLinkAsync(user, user.Email, link);
        return HttpResult.NoContent();
    }
}
