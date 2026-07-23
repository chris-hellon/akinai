using InvoiceSoftware.Domain.Entities;
using InvoiceSoftware.Domain.Enums;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Infrastructure.Hubs;
using Microsoft.AspNetCore.SignalR;
using InvoiceSoftware.Shared.Billing;
using InvoiceSoftware.Shared.Dtos.Billing;
using InvoiceSoftware.Shared.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InvoiceSoftware.Infrastructure.Services.Billing;

/// <inheritdoc />
public class SubscriptionService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    IBillingProvider provider,
    INotificationService notifications,
    IHubContext<NotificationHub> hub,
    IUserEventBus bus,
    ILogger<SubscriptionService> logger,
    IPlanCatalog catalog) : ISubscriptionService
{
    /// <summary>How long a checkout stays open before it has to be started again.</summary>
    private static readonly TimeSpan CheckoutValidFor = TimeSpan.FromMinutes(30);

    public async Task<SubscriptionDto> GetAsync(string userId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException($"No such user '{userId}'.");
        return await ToDto(user, cancellationToken);
    }

    public async Task<IReadOnlyList<SubscriptionEventDto>> GetHistoryAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.SubscriptionEvents
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.OccurredAtUtc)
            .Select(e => new SubscriptionEventDto(
                e.OccurredAtUtc, e.Type, e.FromPlan, e.ToPlan, e.Amount, e.Currency, e.Simulated, e.Note))
            .ToListAsync(cancellationToken);
    }

    public async Task<CheckoutSessionDto> StartCheckoutAsync(
        string userId, BillingPlan plan, BillingCycle cycle, CancellationToken cancellationToken = default)
    {
        var definition = await catalog.ForAsync(plan, cancellationToken);
        if (!definition.IsPaid)
            throw new InvalidOperationException("The free plan does not go through checkout.");

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Priced here, from the catalogue — never from the request.
        var amount = definition.PriceFor(cycle);

        var session = CheckoutSession.Create(
            userId, plan.ToString(), cycle.ToString(), amount, definition.Currency,
            provider.IsSimulated, CheckoutValidFor);

        db.CheckoutSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        var url = await provider.CreateCheckoutUrlAsync(
            session.Id, plan, cycle, amount, definition.Currency, cancellationToken);

        return new CheckoutSessionDto(
            session.Id, plan, definition.Name, cycle, amount, definition.Currency,
            definition.CurrencySymbol, session.ExpiresAtUtc, provider.IsSimulated, url);
    }

    public async Task<CheckoutResultDto> CompleteCheckoutAsync(
        string userId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Scoped to the caller, so one account can never complete another's checkout.
        var session = await db.CheckoutSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, cancellationToken);

        if (session is null)
            return new CheckoutResultDto(false, "That checkout could not be found.", null);
        if (session.IsCompleted)
            return new CheckoutResultDto(false, "That checkout has already been completed.", null);
        if (session.IsExpired(DateTime.UtcNow))
            return new CheckoutResultDto(false, "That checkout has expired. Please start again.", null);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
            return new CheckoutResultDto(false, "Account not found.", null);

        var plan = Enum.Parse<BillingPlan>(session.Plan);
        var cycle = Enum.Parse<BillingCycle>(session.Cycle);
        var fromPlan = user.Plan;

        session.Complete();

        var now = DateTime.UtcNow;
        user.Plan = plan;
        user.SubscriptionStatus = SubscriptionStatus.Active;
        user.BillingCycle = cycle;
        // Extends rather than replaces, so upgrading mid-period does not throw
        // away time the account has already paid for.
        var startFrom = user.CurrentPeriodEndUtc > now ? user.CurrentPeriodEndUtc!.Value : now;
        user.CurrentPeriodEndUtc = AddPeriod(startFrom, cycle);
        user.PlanSelectedAtUtc ??= now;

        db.SubscriptionEvents.Add(SubscriptionEvent.Create(
            userId,
            type: fromPlan == plan ? "Renewed" : "Subscribed",
            fromPlan: fromPlan.ToString(),
            toPlan: plan.ToString(),
            cycle: cycle.ToString(),
            amount: session.Amount,
            currency: session.Currency,
            simulated: session.Simulated,
            note: session.Simulated ? "Simulated payment — no money was taken." : null));

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Subscription activated for {UserId}: {From} -> {To} ({Cycle}), simulated={Simulated}.",
            userId, fromPlan, plan, cycle, session.Simulated);

        var renewal = user.CurrentPeriodEndUtc?.ToString("d MMMM yyyy");
        await AnnounceAsync(userId,
            fromPlan == plan ? $"{(await catalog.ForAsync(plan, cancellationToken)).Name} renewed" : $"You're on Akinai {(await catalog.ForAsync(plan, cancellationToken)).Name}",
            fromPlan == plan
                ? $"Your subscription runs until {renewal}."
                : $"Every currency, all eight languages, bank import and recurring billing are unlocked. Renews {renewal}.",
            cancellationToken);

        return new CheckoutResultDto(true, null, await ToDto(user, cancellationToken));
    }

    public async Task<SubscriptionDto> SelectFreeAsync(string userId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException($"No such user '{userId}'.");

        // Only records the choice. An account with time left on a paid plan
        // keeps it — choosing free here is not a way to cancel by surprise.
        user.PlanSelectedAtUtc ??= DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return await ToDto(user, cancellationToken);
    }

    public async Task<SubscriptionDto> CancelAsync(string userId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException($"No such user '{userId}'.");

        if (user.SubscriptionStatus is not (SubscriptionStatus.Active or SubscriptionStatus.Cancelling))
            return await ToDto(user, cancellationToken);

        user.SubscriptionStatus = SubscriptionStatus.Cancelling;

        db.SubscriptionEvents.Add(SubscriptionEvent.Create(
            userId, "Cancelled", user.Plan.ToString(), user.Plan.ToString(),
            cycle: user.BillingCycle?.ToString(),
            note: $"Access continues until {user.CurrentPeriodEndUtc:yyyy-MM-dd}."));

        await db.SaveChangesAsync(cancellationToken);

        await AnnounceAsync(userId,
            "Subscription cancelled",
            $"You keep {(await catalog.ForAsync(user.Plan, cancellationToken)).Name} until {user.CurrentPeriodEndUtc:d MMMM yyyy}, then move to Free. Nothing you've created is removed.",
            cancellationToken);

        return await ToDto(user, cancellationToken);
    }

    public async Task<SubscriptionDto> ResumeAsync(string userId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException($"No such user '{userId}'.");

        // Only meaningful while the paid period is still running; once it has
        // lapsed the account has to check out again.
        if (user.SubscriptionStatus != SubscriptionStatus.Cancelling
            || user.CurrentPeriodEndUtc is null || user.CurrentPeriodEndUtc <= DateTime.UtcNow)
            return await ToDto(user, cancellationToken);

        user.SubscriptionStatus = SubscriptionStatus.Active;

        db.SubscriptionEvents.Add(SubscriptionEvent.Create(
            userId, "Resumed", user.Plan.ToString(), user.Plan.ToString(), user.BillingCycle?.ToString()));

        await db.SaveChangesAsync(cancellationToken);

        await AnnounceAsync(userId,
            "Subscription resumed",
            $"Your {(await catalog.ForAsync(user.Plan, cancellationToken)).Name} plan will renew on {user.CurrentPeriodEndUtc:d MMMM yyyy}.",
            cancellationToken);

        return await ToDto(user, cancellationToken);
    }

    public async Task<SubscriptionDto> AdminSetPlanAsync(
        string userId, BillingPlan plan, BillingCycle? cycle, int? months,
        string performedByEmail, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException($"No such user '{userId}'.");

        var fromPlan = user.Plan;
        var now = DateTime.UtcNow;

        if (plan == BillingPlan.Free)
        {
            user.Plan = BillingPlan.Free;
            user.SubscriptionStatus = SubscriptionStatus.None;
            user.BillingCycle = null;
            user.CurrentPeriodEndUtc = null;
        }
        else
        {
            var effectiveCycle = cycle ?? BillingCycle.Monthly;
            user.Plan = plan;
            user.SubscriptionStatus = SubscriptionStatus.Active;
            user.BillingCycle = effectiveCycle;
            user.CurrentPeriodEndUtc = months is int m
                ? now.AddMonths(m)
                : AddPeriod(now, effectiveCycle);
        }

        user.PlanSelectedAtUtc ??= now;

        db.SubscriptionEvents.Add(SubscriptionEvent.Create(
            userId, "AdminOverride", fromPlan.ToString(), plan.ToString(),
            cycle: user.BillingCycle?.ToString(),
            simulated: true,
            note: $"Set by admin {performedByEmail}. No payment taken."));

        await db.SaveChangesAsync(cancellationToken);

        logger.LogWarning(
            "Admin {Admin} set plan for {UserId}: {From} -> {To}.", performedByEmail, userId, fromPlan, plan);

        // Worded for the customer, not the admin: they did not do this, so say
        // what changed rather than who changed it.
        await AnnounceAsync(userId,
            $"Your plan changed to {(await catalog.ForAsync(plan, cancellationToken)).Name}",
            plan == BillingPlan.Free
                ? "Your account is now on the Free plan. Everything you've created stays available."
                : $"Your account is now on {(await catalog.ForAsync(plan, cancellationToken)).Name} until {user.CurrentPeriodEndUtc:d MMMM yyyy}.",
            cancellationToken);

        return await ToDto(user, cancellationToken);
    }


    /// <summary>
    /// Tells the user their plan changed, and tells their open browser tabs.
    ///
    /// Two separate things, deliberately. The notification is a durable record
    /// the user can read later. The "entitlements" push is ephemeral: it tells
    /// any tab that is already open to re-read what it is allowed to do, so a
    /// session that upgraded in one tab does not keep enforcing the old plan in
    /// another until it is refreshed.
    ///
    /// Neither is allowed to fail the transition — the plan has already been
    /// committed by the time this runs, and a dropped notification is not worth
    /// rolling a paid subscription back for.
    /// </summary>
    private async Task AnnounceAsync(
        string userId, string title, string message, CancellationToken cancellationToken)
    {
        try
        {
            await notifications.CreateAsync(
                userId, NotificationType.SubscriptionChanged, title, message,
                link: "settings/billing", cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not raise the subscription notification for {UserId}.", userId);
        }

        try
        {
            // SendCoreAsync, not SendAsync: with no arguments the token would be
            // bound as the first message argument.
            await hub.Clients.User(userId)
                .SendCoreAsync("entitlements", [], cancellationToken);
            // And in-process, for circuits that hold no hub connection.
            bus.PublishEntitlementsChanged(userId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not push the entitlement change to {UserId}.", userId);
        }
    }

    private static DateTime AddPeriod(DateTime from, BillingCycle cycle)
        => cycle == BillingCycle.Annual ? from.AddYears(1) : from.AddMonths(1);

    private async Task<SubscriptionDto> ToDto(ApplicationUser user, CancellationToken cancellationToken)
    {
        // Reported against the effective plan, so a lapsed account is not shown
        // as though it still has what it has stopped paying for.
        var effective = EntitlementService.ResolveEffectivePlan(
            user.Plan, user.SubscriptionStatus, user.CurrentPeriodEndUtc, DateTime.UtcNow);
        var definition = await catalog.ForAsync(effective, cancellationToken);

        return new SubscriptionDto(
            effective,
            definition.Name,
            user.SubscriptionStatus,
            user.BillingCycle,
            user.CurrentPeriodEndUtc,
            user.SubscriptionStatus == SubscriptionStatus.Cancelling,
            user.SubscriptionStatus == SubscriptionStatus.Active && user.BillingCycle is BillingCycle c
                ? definition.PriceFor(c)
                : null,
            definition.Currency,
            definition.CurrencySymbol,
            provider.IsSimulated,
            provider.Name);
    }
}
