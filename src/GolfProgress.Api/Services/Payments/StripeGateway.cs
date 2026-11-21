using GolfProgress.Api.Configuration;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace GolfProgress.Api.Services.Payments;

/// <summary>
/// The calls the API makes to Stripe (#310), behind an interface so tests can stand in for
/// Stripe. Everything else about subscriptions is in <see cref="StripeSubscriptions"/>.
/// </summary>
public interface IStripeGateway
{
    Task<string> CreateCustomerAsync(Guid userId, string? email, string? name, CancellationToken ct);

    /// <returns>The URL of Stripe's hosted checkout page.</returns>
    Task<string> CreateCheckoutAsync(SessionCreateOptions options, CancellationToken ct);

    /// <returns>The URL of the Customer Portal.</returns>
    Task<string> CreatePortalAsync(string customerId, string returnUrl, string? locale, CancellationToken ct);

    /// <summary>The subscription as Stripe has it now; null when there's no such subscription.</summary>
    Task<Stripe.Subscription?> GetSubscriptionAsync(string subscriptionId, CancellationToken ct);

    Task CancelSubscriptionAsync(string subscriptionId, CancellationToken ct);

    /// <summary>A price's amount in cents, and its currency.</summary>
    Task<(long Amount, string Currency)> GetPriceAsync(string priceId, CancellationToken ct);

    /// <summary>The event in a webhook, after checking its signature; throws when it isn't Stripe's.</summary>
    Event ParseWebhook(string json, string signatureHeader, string secret);
}

public sealed class StripeGateway : IStripeGateway
{
    private readonly StripeClient? _client;

    public StripeGateway(IOptions<PaymentsOptions> options)
    {
        var key = options.Value.Stripe.SecretKey;
        _client = string.IsNullOrWhiteSpace(key) ? null : new StripeClient(key);
    }

    private StripeClient Client => _client ?? throw new InvalidOperationException("Stripe isn't configured: set Payments:Stripe:SecretKey.");

    public async Task<string> CreateCustomerAsync(Guid userId, string? email, string? name, CancellationToken ct)
    {
        var customer = await new CustomerService(Client).CreateAsync(new CustomerCreateOptions
        {
            Email = email,
            Name = name,
            Metadata = new Dictionary<string, string> { ["userId"] = userId.ToString() },
        }, cancellationToken: ct);
        return customer.Id;
    }

    public async Task<string> CreateCheckoutAsync(SessionCreateOptions options, CancellationToken ct) =>
        (await new SessionService(Client).CreateAsync(options, cancellationToken: ct)).Url;

    public async Task<string> CreatePortalAsync(string customerId, string returnUrl, string? locale, CancellationToken ct) =>
        (await new Stripe.BillingPortal.SessionService(Client).CreateAsync(new Stripe.BillingPortal.SessionCreateOptions
        {
            Customer = customerId,
            ReturnUrl = returnUrl,
            Locale = locale,
        }, cancellationToken: ct)).Url;

    public async Task<Stripe.Subscription?> GetSubscriptionAsync(string subscriptionId, CancellationToken ct)
    {
        try
        {
            return await new SubscriptionService(Client).GetAsync(subscriptionId, cancellationToken: ct);
        }
        catch (StripeException e) when (e.StripeError?.Code == "resource_missing")
        {
            return null;
        }
    }

    public async Task CancelSubscriptionAsync(string subscriptionId, CancellationToken ct)
    {
        try
        {
            await new SubscriptionService(Client).CancelAsync(subscriptionId, cancellationToken: ct);
        }
        catch (StripeException e) when (e.StripeError?.Code == "resource_missing")
        {
            // Gone already: what was asked for has happened.
        }
    }

    public async Task<(long Amount, string Currency)> GetPriceAsync(string priceId, CancellationToken ct)
    {
        var price = await new PriceService(Client).GetAsync(priceId, cancellationToken: ct);
        return (price.UnitAmount ?? 0, price.Currency);
    }

    // The library is pinned to one API version; the webhook endpoint may send another, and
    // only the subscription id is read from the event, so a mismatch doesn't matter.
    public Event ParseWebhook(string json, string signatureHeader, string secret) =>
        EventUtility.ConstructEvent(json, signatureHeader, secret, throwOnApiVersionMismatch: false);
}
