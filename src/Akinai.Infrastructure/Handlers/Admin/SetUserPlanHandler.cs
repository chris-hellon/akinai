using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Shared.Api.Admin;
using Akinai.Shared.Services;

namespace Akinai.Infrastructure.Handlers.Admin;

/// <summary>
/// Grants or removes a plan without payment.
///
/// The role is re-checked here rather than left to the middleware alone, in
/// keeping with the rest of the admin handlers — this one hands out paid
/// features, so it is the last place to be relaxed about who is calling.
/// </summary>
public class SetUserPlanHandler(
    ICurrentUserService currentUserService,
    ISubscriptionService subscriptions) : IHandle<SetUserPlan>
{
    public async Task<HttpResult> Handle(SetUserPlan request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsInRole(AdminRoles.Admin)) return HttpResult.Forbidden();

        if (string.IsNullOrWhiteSpace(request.UserId)) return HttpResult.BadRequest();
        if (request.Months is < 1 or > 120) return HttpResult.BadRequest();

        await subscriptions.AdminSetPlanAsync(
            request.UserId,
            request.Plan,
            request.Cycle,
            request.Months,
            currentUserService.Email ?? "unknown",
            cancellationToken);

        return HttpResult.NoContent();
    }
}
