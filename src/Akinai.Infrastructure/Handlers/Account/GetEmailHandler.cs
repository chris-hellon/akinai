using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Account;
using Akinai.Shared.Dtos.Account;
using Akinai.Shared.Services;
using Microsoft.AspNetCore.Identity;

namespace Akinai.Infrastructure.Handlers.Account;

public class GetEmailHandler(
    UserManager<ApplicationUser> userManager,
    ICurrentUserService currentUserService) : IHandle<GetEmail, EmailDto>
{
    public async Task<HttpResult<EmailDto>> Handle(GetEmail request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<EmailDto>.Unauthorized();

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
            return HttpResult<EmailDto>.NotFound();

        var email = await userManager.GetEmailAsync(user) ?? "";
        var isConfirmed = await userManager.IsEmailConfirmedAsync(user);

        return HttpResult<EmailDto>.Ok(new EmailDto(email, isConfirmed));
    }
}
