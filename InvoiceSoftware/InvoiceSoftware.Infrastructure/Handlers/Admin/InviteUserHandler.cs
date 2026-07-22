using System.Text;
using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Shared.Api.Admin;
using InvoiceSoftware.Shared.Dtos.Admin;
using InvoiceSoftware.Shared.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace InvoiceSoftware.Infrastructure.Handlers.Admin;

/// <summary>
/// Creates an account with no password and emails a set-password link.
///
/// The admin never chooses or sees the user's password — possession of the
/// emailed link is what grants access. The link is also returned so the admin
/// has a fallback when mail doesn't arrive; it is a credential in its own
/// right and should be sent over a trusted channel.
/// </summary>
public class InviteUserHandler(
    UserManager<ApplicationUser> userManager,
    IEmailSender<ApplicationUser> emailSender,
    IHttpContextAccessor httpContextAccessor,
    ICurrentUserService currentUserService,
    ILogger<InviteUserHandler> logger)
    : IHandle<InviteUser, InviteResultDto>
{
    public async Task<HttpResult<InviteResultDto>> Handle(InviteUser request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsInRole(AdminRoles.Admin))
            return HttpResult<InviteResultDto>.Forbidden();

        var email = request.Email?.Trim() ?? "";
        if (email.Length == 0 || !email.Contains('@'))
            return HttpResult<InviteResultDto>.BadRequest(null!);

        if (await userManager.FindByEmailAsync(email) is not null)
            return HttpResult<InviteResultDto>.BadRequest(null!);

        var user = new ApplicationUser { UserName = email, Email = email };
        var created = await userManager.CreateAsync(user);   // no password — set via the link
        if (!created.Succeeded)
            return HttpResult<InviteResultDto>.BadRequest(null!);

        // The admin is vouching for the address, and access still requires the
        // link that was mailed to it — so confirm now rather than making the
        // invitee complete two separate email round-trips before they can sign
        // in (RequireConfirmedAccount would otherwise block them).
        var confirmToken = await userManager.GenerateEmailConfirmationTokenAsync(user);
        await userManager.ConfirmEmailAsync(user, confirmToken);

        // A password-reset token is how a user with no password sets their first one.
        var code = await userManager.GeneratePasswordResetTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

        var link = BuildLink("account/reset-password", new() { ["code"] = code });

        var emailSent = true;
        string? emailError = null;
        try
        {
            await emailSender.SendPasswordResetLinkAsync(user, email, link);
        }
        catch (Exception ex)
        {
            // The account exists either way — surface the failure so the admin
            // knows to pass the link on manually rather than assuming it sent.
            emailSent = false;
            emailError = ex.Message;
            logger.LogError(ex, "Invite email to {Email} failed; the account was still created.", email);
        }

        return HttpResult<InviteResultDto>.Ok(
            new InviteResultDto(user.Id, email, emailSent, emailError, link));
    }

    private string BuildLink(string path, Dictionary<string, string?> query)
    {
        var req = httpContextAccessor.HttpContext?.Request;
        var baseUrl = req is null ? "" : $"{req.Scheme}://{req.Host}";
        return QueryHelpers.AddQueryString($"{baseUrl}/{path}", query);
    }
}
