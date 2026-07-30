using Akinai.Shared.Billing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe.Checkout;

namespace Akinai.Infrastructure.Services.Billing;

/// <summary>
/// Sends the browser to Stripe's hosted checkout.
///
/// The subscription is not granted here. Stripe confirms payment by webhook,
/// and only that webhook calls CompleteCheckoutAsync — because the browser
/// returning to a success URL proves nothing. A user who closes the tab after
/// paying must still be upgraded, and a user who forges a redirect must not be.
/// </summary>
public class StripeBillingProvider(
    IOptions<StripeOptions> options,
    IHttpContextAccessor httpContextAccessor,
    ILogger<StripeBillingProvider> logger) : IBillingProvider
{
    private readonly StripeOptions _options = options.Value;

    /// <summary>
    /// Stripe tax code for business-use SaaS.
    ///
    /// Held here rather than in config: it is a property of what Akinai *is*,
    /// not of a deployment, and getting it wrong misfiles tax rather than
    /// failing loudly. Change it only on advice.
    /// </summary>
    private const string TaxCode = "txcd_10103001";

    public string Name => _options.IsTestMode ? "Stripe (test mode)" : "Stripe";

    /// <summary>
    /// Test-mode keys move no real money, so the UI should still warn. Live keys
    /// are the only case where a charge is genuine.
    /// </summary>
    public bool IsSimulated => _options.IsTestMode;

    /// <summary>
    /// Always. A test-mode subscription is still a real recurring subscription —
    /// Stripe advances it with test clocks — so it must never be offered a
    /// manual extension.
    /// </summary>
    public bool RenewsAutomatically => true;

    public async Task<string> CreateCheckoutUrlAsync(
        Guid sessionId, BillingPlan plan, BillingCycle cycle, decimal amount, string currency,
        CancellationToken cancellationToken = default)
    {
        var service = new SessionService(new Stripe.StripeClient(_options.SecretKey));

        var session = await service.CreateAsync(new SessionCreateOptions
        {
            Mode = "subscription",

            // Priced from our own catalogue rather than a Stripe Price object.
            // The catalogue is admin-editable and is what the entitlement checks
            // read; syncing a second copy of every price into Stripe would give
            // us two sources of truth and no way to tell which was right.
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = currency.ToLowerInvariant(),
                        // Stripe works in the smallest unit — pence, not pounds.
                        UnitAmount = (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero),
                        Recurring = new SessionLineItemPriceDataRecurringOptions
                        {
                            Interval = cycle == BillingCycle.Annual ? "year" : "month",
                        },
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"Akinai {plan}",
                            Description = cycle == BillingCycle.Annual ? "Annual subscription" : "Monthly subscription",

                            // Required by Stripe Managed Payments, which is on by
                            // default. It is what lets Stripe work out and remit
                            // VAT/GST per country — which matters here, because
                            // the customers are freelancers scattered across
                            // jurisdictions and the alternative is registering
                            // for tax in each one yourself.
                            //
                            // txcd_10103001 = Software as a Service, business use.
                            // Akinai is sold to people running a business, not to
                            // consumers, and several countries tax the two
                            // differently.
                            TaxCode = TaxCode,
                        },
                    },
                },
            ],

            // Our own session id travels with the payment and comes back on the
            // webhook. It is how a Stripe event is matched to the checkout we
            // priced and recorded — without it we would be trusting amounts
            // reported back to us.
            ClientReferenceId = sessionId.ToString(),
            Metadata = new Dictionary<string, string>
            {
                ["akinai_session_id"] = sessionId.ToString(),
                ["akinai_plan"] = plan.ToString(),
                ["akinai_cycle"] = cycle.ToString(),
            },

            // Stripe requires absolute URLs. Built from the live request so
            // this follows the deployment without a hardcoded domain — the same
            // reason invoice links are request-derived.
            SuccessUrl = $"{Origin()}/billing/checkout/{sessionId}?paid=1",
            CancelUrl = $"{Origin()}/settings/billing?cancelled=1",
        }, cancellationToken: cancellationToken);

        logger.LogInformation("Stripe checkout session {StripeId} created for checkout {SessionId}.",
            session.Id, sessionId);

        return session.Url;
    }

    /// <summary>
    /// The public origin of the current request.
    ///
    /// Behind a proxy this needs UseForwardedHeaders, or Stripe will redirect
    /// the customer to an internal hostname after they have paid.
    /// </summary>
    private string Origin()
    {
        var request = httpContextAccessor.HttpContext?.Request
            ?? throw new InvalidOperationException("Checkout must be started from an HTTP request.");
        return $"{request.Scheme}://{request.Host}";
    }
}
