using System.Text;
using System.Text.Encodings.Web;
using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Account;
using Akinai.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace Akinai.Infrastructure.Handlers.Account;

public class ChangeEmailHandler(
    UserManager<ApplicationUser> userManager,
    IEmailSender<ApplicationUser> emailSender,
    ICurrentUserService currentUserService,
    IHttpContextAccessor httpContextAccessor) : IHandle<ChangeEmail>
{
    public async Task<HttpResult> Handle(ChangeEmail request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult.Unauthorized();

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
            return HttpResult.NotFound();

        var currentEmail = await userManager.GetEmailAsync(user);
        if (request.NewEmail == currentEmail)
            throw new InvalidOperationException("Your email is unchanged.");

        var code = await userManager.GenerateChangeEmailTokenAsync(user, request.NewEmail);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

        var httpContext = httpContextAccessor.HttpContext;
        var baseUrl = $"{httpContext?.Request.Scheme}://{httpContext?.Request.Host}";
        var callbackUrl = $"{baseUrl}/account/confirm-email-change?userId={userId}&email={Uri.EscapeDataString(request.NewEmail)}&code={code}";

        await emailSender.SendConfirmationLinkAsync(user, request.NewEmail, HtmlEncoder.Default.Encode(callbackUrl));

        return HttpResult.NoContent();
    }
}
