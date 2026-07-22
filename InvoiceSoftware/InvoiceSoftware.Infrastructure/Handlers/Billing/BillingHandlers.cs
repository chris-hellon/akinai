using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Shared.Api.Billing;
using InvoiceSoftware.Shared.Billing;
using InvoiceSoftware.Shared.Dtos.Billing;
using InvoiceSoftware.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Infrastructure.Handlers.Billing;

public class GetEntitlementsHandler(
    ICurrentUserService currentUserService,
    IEntitlementService entitlements) : IHandle<GetEntitlements, EntitlementsDto>
{
    public async Task<HttpResult<EntitlementsDto>> Handle(GetEntitlements request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult<EntitlementsDto>.Unauthorized();

        return HttpResult<EntitlementsDto>.Ok(await entitlements.GetAsync(userId, cancellationToken));
    }
}

public class GetSubscriptionHandler(
    ICurrentUserService currentUserService,
    ISubscriptionService subscriptions) : IHandle<GetSubscription, SubscriptionDto>
{
    public async Task<HttpResult<SubscriptionDto>> Handle(GetSubscription request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult<SubscriptionDto>.Unauthorized();

        return HttpResult<SubscriptionDto>.Ok(await subscriptions.GetAsync(userId, cancellationToken));
    }
}

public class GetBillingHistoryHandler(
    ICurrentUserService currentUserService,
    ISubscriptionService subscriptions) : IHandle<GetBillingHistory, List<SubscriptionEventDto>>
{
    public async Task<HttpResult<List<SubscriptionEventDto>>> Handle(
        GetBillingHistory request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult<List<SubscriptionEventDto>>.Unauthorized();

        var history = await subscriptions.GetHistoryAsync(userId, cancellationToken);
        return HttpResult<List<SubscriptionEventDto>>.Ok(history.ToList());
    }
}

public class StartCheckoutHandler(
    ICurrentUserService currentUserService,
    ISubscriptionService subscriptions) : IHandle<StartCheckout, CheckoutSessionDto>
{
    public async Task<HttpResult<CheckoutSessionDto>> Handle(StartCheckout request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult<CheckoutSessionDto>.Unauthorized();

        if (!Plans.For(request.Plan).IsPaid)
            return HttpResult<CheckoutSessionDto>.BadRequest(null!);

        var session = await subscriptions.StartCheckoutAsync(userId, request.Plan, request.Cycle, cancellationToken);
        return HttpResult<CheckoutSessionDto>.Ok(session);
    }
}

public class GetCheckoutHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService,
    Services.Billing.IBillingProvider provider) : IHandle<GetCheckout, CheckoutSessionDto>
{
    public async Task<HttpResult<CheckoutSessionDto>> Handle(GetCheckout request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult<CheckoutSessionDto>.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Scoped to the caller: a session id is not a capability to view
        // somebody else's checkout.
        var session = await db.CheckoutSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.SessionId && s.UserId == userId, cancellationToken);

        if (session is null) return HttpResult<CheckoutSessionDto>.NotFound();

        var plan = Enum.Parse<BillingPlan>(session.Plan);
        var cycle = Enum.Parse<BillingCycle>(session.Cycle);
        var definition = Plans.For(plan);

        return HttpResult<CheckoutSessionDto>.Ok(new CheckoutSessionDto(
            session.Id, plan, definition.Name, cycle, session.Amount, session.Currency,
            definition.CurrencySymbol, session.ExpiresAtUtc, session.Simulated,
            $"billing/checkout/{session.Id}"));
    }
}

public class CompleteCheckoutHandler(
    ICurrentUserService currentUserService,
    ISubscriptionService subscriptions) : IHandle<CompleteCheckout, CheckoutResultDto>
{
    public async Task<HttpResult<CheckoutResultDto>> Handle(
        CompleteCheckout request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult<CheckoutResultDto>.Unauthorized();

        var result = await subscriptions.CompleteCheckoutAsync(userId, request.SessionId, cancellationToken);
        return HttpResult<CheckoutResultDto>.Ok(result);
    }
}

public class SelectFreePlanHandler(
    ICurrentUserService currentUserService,
    ISubscriptionService subscriptions) : IHandle<SelectFreePlan, SubscriptionDto>
{
    public async Task<HttpResult<SubscriptionDto>> Handle(SelectFreePlan request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult<SubscriptionDto>.Unauthorized();

        return HttpResult<SubscriptionDto>.Ok(await subscriptions.SelectFreeAsync(userId, cancellationToken));
    }
}

public class CancelSubscriptionHandler(
    ICurrentUserService currentUserService,
    ISubscriptionService subscriptions) : IHandle<CancelSubscription, SubscriptionDto>
{
    public async Task<HttpResult<SubscriptionDto>> Handle(
        CancelSubscription request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult<SubscriptionDto>.Unauthorized();

        return HttpResult<SubscriptionDto>.Ok(await subscriptions.CancelAsync(userId, cancellationToken));
    }
}

public class ResumeSubscriptionHandler(
    ICurrentUserService currentUserService,
    ISubscriptionService subscriptions) : IHandle<ResumeSubscription, SubscriptionDto>
{
    public async Task<HttpResult<SubscriptionDto>> Handle(
        ResumeSubscription request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult<SubscriptionDto>.Unauthorized();

        return HttpResult<SubscriptionDto>.Ok(await subscriptions.ResumeAsync(userId, cancellationToken));
    }
}
