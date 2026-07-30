using Akinai.Infrastructure.Data;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;

namespace Akinai.Infrastructure.Services.Billing;

/// <summary>
/// Turns a verified Stripe event into a subscription change.
///
/// This is the only place a paid plan is granted. The browser returning to a
/// success URL is not evidence of payment — it can be typed by hand — so the
/// success page shows a confirmation and nothing more.
/// </summary>
public class StripeWebhookHandler(
    IOptions<StripeOptions> options,
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ISubscriptionService subscriptions,
    ILogger<StripeWebhookHandler> logger)
{
    private readonly StripeOptions _options = options.Value;

    /// <summary>
    /// Verifies the signature and applies the event.
    /// </summary>
    /// <returns>False when the payload could not be verified — the caller must return 400.</returns>
    public async Task<bool> HandleAsync(string payload, string? signatureHeader, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookSecret))
        {
            // Refusing is the safe failure. An unverified webhook endpoint is a
            // free upgrade for anyone who can find the URL.
            logger.LogError("Stripe webhook received but no signing secret is configured; refusing.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(signatureHeader))
        {
            logger.LogWarning("Stripe webhook arrived with no Stripe-Signature header.");
            return false;
        }

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(payload, signatureHeader, _options.WebhookSecret);
        }
        catch (Exception ex)
        {
            // Deliberately broad. A malformed body throws JSON exceptions rather
            // than StripeException, and an unhandled one here would surface as a
            // 500 — which reads to Stripe as "retry later" instead of "this
            // payload is bad", so it would retry a hopeless delivery for days.
            logger.LogWarning(ex, "Rejected an unverifiable Stripe webhook.");
            return false;
        }

        // A subscription is not one event but a lifecycle. Handling only the
        // first payment means the account is charged every month and still
        // lapses to free the day the first period ends.
        switch (stripeEvent.Type)
        {
            case EventTypes.InvoicePaid:
                await OnInvoicePaidAsync(stripeEvent, cancellationToken);
                return true;

            case EventTypes.InvoicePaymentFailed:
                await OnPaymentFailedAsync(stripeEvent, cancellationToken);
                return true;

            case EventTypes.CustomerSubscriptionDeleted:
                await OnSubscriptionEndedAsync(stripeEvent, cancellationToken);
                return true;

            case EventTypes.CheckoutSessionCompleted:
                break;   // handled below

            default:
                logger.LogDebug("Ignoring Stripe event {Type}.", stripeEvent.Type);
                return true;
        }

        if (stripeEvent.Data.Object is not Stripe.Checkout.Session session)
        {
            logger.LogWarning("checkout.session.completed carried no session object.");
            return true;
        }

        // Our own checkout id, sent when the session was created. Everything
        // priced and recorded on our side hangs off this — the amounts Stripe
        // reports back are never used to decide what was bought.
        var reference = session.ClientReferenceId
            ?? (session.Metadata is not null && session.Metadata.TryGetValue("akinai_session_id", out var m) ? m : null);

        if (!Guid.TryParse(reference, out var checkoutId))
        {
            logger.LogWarning("Stripe session {StripeId} had no usable Akinai reference.", session.Id);
            return true;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var checkout = await db.CheckoutSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == checkoutId, cancellationToken);

        if (checkout is null)
        {
            logger.LogWarning("Stripe referenced checkout {CheckoutId}, which does not exist.", checkoutId);
            return true;
        }

        // Stripe retries until it gets a 2xx, so the same event can arrive more
        // than once. CompleteCheckoutAsync already refuses a completed session,
        // but checking here keeps a retry out of the warning logs.
        if (checkout.IsCompleted)
        {
            logger.LogInformation("Checkout {CheckoutId} was already completed; ignoring the retry.", checkoutId);
            return true;
        }

        var result = await subscriptions.CompleteCheckoutAsync(checkout.UserId, checkoutId, cancellationToken);

        // Remember Stripe's subscription id. Every later event — renewal, failed
        // payment, cancellation — identifies the account by this and nothing
        // else, so without it the subscription becomes unmatchable.
        if (result.Succeeded && !string.IsNullOrEmpty(session.SubscriptionId))
            await subscriptions.LinkProviderSubscriptionAsync(checkout.UserId, session.SubscriptionId, cancellationToken);

        if (result.Succeeded)
            logger.LogInformation("Checkout {CheckoutId} completed from Stripe event {EventId}.", checkoutId, stripeEvent.Id);
        else
            logger.LogWarning("Checkout {CheckoutId} could not be completed: {Reason}", checkoutId, result.Error);

        // Acknowledged either way. A failure here is ours to investigate, and
        // making Stripe retry a checkout we have already rejected helps nobody.
        return true;
    }

    /// <summary>A recurring payment succeeded — extend the paid period.</summary>
    private async Task OnInvoicePaidAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        if (stripeEvent.Data.Object is not Invoice invoice) return;

        var subscriptionId = invoice.Parent?.SubscriptionDetails?.SubscriptionId;
        if (string.IsNullOrEmpty(subscriptionId)) return;

        // Stripe reports the period, so proration, retries and billing anchors
        // are its problem rather than something we re-derive and get wrong.
        var periodEnd = invoice.Lines?.Data?.FirstOrDefault()?.Period?.End;
        if (periodEnd is null)
        {
            logger.LogWarning("invoice.paid for {SubscriptionId} carried no period end.", subscriptionId);
            return;
        }

        await subscriptions.RecordRenewalAsync(
            subscriptionId,
            periodEnd.Value.ToUniversalTime(),
            invoice.AmountPaid / 100m,        // Stripe works in the smallest unit
            invoice.Currency.ToUpperInvariant(),
            cancellationToken);
    }

    private async Task OnPaymentFailedAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        if (stripeEvent.Data.Object is not Invoice invoice) return;
        var subscriptionId = invoice.Parent?.SubscriptionDetails?.SubscriptionId;
        if (!string.IsNullOrEmpty(subscriptionId))
            await subscriptions.MarkPastDueAsync(subscriptionId, cancellationToken);
    }

    private async Task OnSubscriptionEndedAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        if (stripeEvent.Data.Object is not Subscription subscription) return;
        await subscriptions.EndSubscriptionAsync(subscription.Id, cancellationToken);
    }
}
