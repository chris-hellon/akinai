using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Admin;
using Akinai.Shared.Services;
using Microsoft.AspNetCore.Identity;

namespace Akinai.Infrastructure.Handlers.Admin;

public class SetUserAdminHandler(
    UserManager<ApplicationUser> userManager,
    ICurrentUserService currentUserService)
    : IHandle<SetUserAdmin>
{
    public async Task<HttpResult> Handle(SetUserAdmin request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsInRole(AdminRoles.Admin))
            return HttpResult.Forbidden();

        // Revoking your own admin would leave you unable to undo it.
        if (request.UserId == currentUserService.UserId && !request.IsAdmin)
            return HttpResult.BadRequest();

        var user = await userManager.FindByIdAsync(request.UserId);
        if (user is null) return HttpResult.NotFound();

        var isAdmin = await userManager.IsInRoleAsync(user, AdminRoles.Admin);
        if (isAdmin == request.IsAdmin) return HttpResult.NoContent();

        // Never remove the last admin — that would lock everyone out of the area.
        if (!request.IsAdmin)
        {
            var admins = await userManager.GetUsersInRoleAsync(AdminRoles.Admin);
            if (admins.Count <= 1) return HttpResult.BadRequest();
        }

        var result = request.IsAdmin
            ? await userManager.AddToRoleAsync(user, AdminRoles.Admin)
            : await userManager.RemoveFromRoleAsync(user, AdminRoles.Admin);

        return result.Succeeded ? HttpResult.NoContent() : HttpResult.BadRequest();
    }
}
