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

public class SendVerificationEmailHandler(
    UserManager<ApplicationUser> userManager,
    IEmailSender<ApplicationUser> emailSender,
    ICurrentUserService currentUserService,
    IHttpContextAccessor httpContextAccessor) : IHandle<SendVerificationEmail>
{
    public async Task<HttpResult> Handle(SendVerificationEmail request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult.Unauthorized();

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
            return HttpResult.NotFound();

        var email = await userManager.GetEmailAsync(user);
        if (string.IsNullOrEmpty(email))
            throw new InvalidOperationException("No email address found.");

        var code = await userManager.GenerateEmailConfirmationTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

        var httpContext = httpContextAccessor.HttpContext;
        var baseUrl = $"{httpContext?.Request.Scheme}://{httpContext?.Request.Host}";
        var callbackUrl = $"{baseUrl}/account/confirm-email?userId={userId}&code={code}";

        await emailSender.SendConfirmationLinkAsync(user, email, HtmlEncoder.Default.Encode(callbackUrl));

        return HttpResult.NoContent();
    }
}
