using GolfProgress.Api.Configuration;
using GolfProgress.Api.Contracts.Users;
using GolfProgress.Api.ErrorHandling;
using GolfProgress.Api.Security;
using GolfProgress.Api.Services.Payments;
using GolfProgress.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Stripe;

namespace GolfProgress.Api.Controllers;

/// <summary>
/// Subscribing on the web with Stripe (#310, docs/payments.md → "API"). The web app sends
/// the user to Stripe's checkout and Customer Portal; Stripe's webhook tells the API what
/// changed. The plan itself is on GET /me.
/// </summary>
[Authorize]
[ApiController]
public sealed class SubscriptionsController : ControllerBase
{
    private readonly StripeSubscriptions _stripe;

    public SubscriptionsController(StripeSubscriptions stripe) => _stripe = stripe;

    // GET /subscription/prices
    //
    // 503 while subscribing on the web isn't set up.
    [HttpGet("/subscription/prices", Name = "getSubscriptionPrices")]
    [ProducesResponseType<SubscriptionPricesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<SubscriptionPricesResponse>> Prices(CancellationToken ct) =>
        await _stripe.PricesAsync(ct) is { } prices
            ? Ok(new SubscriptionPricesResponse
            {
                Currency = prices.Currency,
                PremiumMonth = prices.PremiumMonth,
                PremiumYear = prices.PremiumYear,
                CoachingMonth = prices.CoachingMonth,
                CoachingYear = prices.CoachingYear,
            })
            : NotAvailable();

    // POST /me/subscription/checkout
    //
    // A Stripe Checkout page for the subscription, with Coaching if asked. 409 for someone
    // subscribed already, here or in Google Play: they change it where they bought it.
    [HttpPost("/me/subscription/checkout", Name = "startCheckout")]
    [ProducesResponseType<RedirectResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<RedirectResponse>> Checkout([FromBody] CheckoutRequest request, CancellationToken ct)
    {
        // By name only: Enum.TryParse would also take "7".
        var period = Enum.GetValues<SubscriptionPeriod>().Cast<SubscriptionPeriod?>()
            .FirstOrDefault(p => string.Equals(p.ToString(), request.Period, StringComparison.OrdinalIgnoreCase));
        if (period is null) return this.FieldError(nameof(request.Period), "period must be Month or Year.");
        var (url, problem) = await _stripe.StartCheckoutAsync(User.GetUserId(), period.Value, request.Coaching, ct);
        return problem switch
        {
            CheckoutProblem.NotAvailable => NotAvailable(),
            CheckoutProblem.AlreadySubscribed => Problem(statusCode: StatusCodes.Status409Conflict, title: "You already have a subscription."),
            _ => Ok(new RedirectResponse { Url = url! }),
        };
    }

    // POST /me/subscription/portal
    //
    // Stripe's Customer Portal: change the period, add or remove Coaching, cancel, invoices.
    // 404 for someone who never subscribed on the web.
    [HttpPost("/me/subscription/portal", Name = "openSubscriptionPortal")]
    [ProducesResponseType<RedirectResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<RedirectResponse>> Portal(CancellationToken ct)
    {
        if (!_stripe.Available) return NotAvailable();
        return await _stripe.PortalAsync(User.GetUserId(), ct) is { } url ? Ok(new RedirectResponse { Url = url }) : NotFound();
    }

    private ObjectResult NotAvailable() =>
        Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Subscribing isn't available yet.");
}

/// <summary>
/// Stripe's webhook (#310): which subscription changed. Anonymous, but every event's
/// signature is checked against the endpoint's secret; anything else is a 400.
/// </summary>
[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class StripeWebhookController : ControllerBase
{
    private readonly StripeSubscriptions _subscriptions;
    private readonly IStripeGateway _stripe;
    private readonly StripeOptions _options;
    private readonly ILogger<StripeWebhookController> _logger;

    public StripeWebhookController(
        StripeSubscriptions subscriptions,
        IStripeGateway stripe,
        IOptions<PaymentsOptions> options,
        ILogger<StripeWebhookController> logger)
    {
        _subscriptions = subscriptions;
        _stripe = stripe;
        _options = options.Value.Stripe;
        _logger = logger;
    }

    // POST /webhooks/stripe
    [AllowAnonymous]
    [HttpPost("/webhooks/stripe")]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookSecret)) return StatusCode(StatusCodes.Status503ServiceUnavailable);

        string json;
        using (var reader = new StreamReader(Request.Body)) json = await reader.ReadToEndAsync(ct);

        Event stripeEvent;
        try
        {
            stripeEvent = _stripe.ParseWebhook(json, Request.Headers["Stripe-Signature"].ToString(), _options.WebhookSecret);
        }
        catch (StripeException e)
        {
            _logger.LogWarning("Refused a Stripe webhook: {Reason}", e.Message);
            return BadRequest();
        }

        var subscriptionId = stripeEvent.Data.Object switch
        {
            Stripe.Subscription s => s.Id,
            Stripe.Checkout.Session { Mode: "subscription" } session => session.SubscriptionId,
            _ => null,
        };
        // Other events need nothing: a failed payment shows as the subscription's status.
        if (subscriptionId is not null) await _subscriptions.SyncAsync(subscriptionId, ct);
        return Ok();
    }
}
