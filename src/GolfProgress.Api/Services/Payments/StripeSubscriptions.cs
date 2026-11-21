using GolfProgress.Api.Configuration;
using GolfProgress.Domain.Entities;
using GolfProgress.Domain.Enums;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Stripe.Checkout;
using DomainSubscription = GolfProgress.Domain.Entities.Subscription;

namespace GolfProgress.Api.Services.Payments;

public enum CheckoutProblem { NotAvailable, AlreadySubscribed }

/// <summary>The prices, in cents, as Stripe has them.</summary>
public sealed record SubscriptionPrices(string Currency, long PremiumMonth, long PremiumYear, long CoachingMonth, long CoachingYear);

/// <summary>
/// Subscribing on the web with Stripe (#310, docs/payments.md → "API"): checkout, the
/// Customer Portal, and keeping <see cref="DomainSubscription"/> rows in step with Stripe.
/// A webhook only says which subscription changed; it's then read from Stripe, so events
/// that arrive twice or out of order leave the same result.
/// </summary>
public sealed class StripeSubscriptions
{
    private readonly GolfProgressDbContext _db;
    private readonly IStripeGateway _stripe;
    private readonly PaymentsOptions _options;
    private readonly PlanService _plans;
    private readonly IMemoryCache _cache;
    private readonly ILogger<StripeSubscriptions> _logger;

    public StripeSubscriptions(
        GolfProgressDbContext db,
        IStripeGateway stripe,
        IOptions<PaymentsOptions> options,
        PlanService plans,
        IMemoryCache cache,
        ILogger<StripeSubscriptions> logger)
    {
        _db = db;
        _stripe = stripe;
        _options = options.Value;
        _plans = plans;
        _cache = cache;
        _logger = logger;
    }

    private StripeOptions Stripe => _options.Stripe;

    public bool Available => Stripe.Configured;

    /// <summary>A Checkout session for the subscription, with Coaching if asked; its URL to send the user to.</summary>
    public async Task<(string? Url, CheckoutProblem? Problem)> StartCheckoutAsync(
        Guid userId, SubscriptionPeriod period, bool coaching, CancellationToken ct)
    {
        if (!Available) return (null, CheckoutProblem.NotAvailable);

        var user = await _db.Users.FirstAsync(u => u.UserId == userId, ct);
        // One subscription at a time, wherever it was bought: changes go through the portal.
        if ((await _plans.ForAsync(user, ct)).Subscription is not null) return (null, CheckoutProblem.AlreadySubscribed);

        if (user.StripeCustomerId is null)
        {
            user.StripeCustomerId = await _stripe.CreateCustomerAsync(user.UserId, user.Email, user.DisplayName, ct);
            await _db.SaveChangesAsync(ct);
        }

        var items = new List<SessionLineItemOptions> { new() { Price = PriceFor(SubscriptionProduct.Premium, period), Quantity = 1 } };
        if (coaching) items.Add(new SessionLineItemOptions { Price = PriceFor(SubscriptionProduct.Coaching, period), Quantity = 1 });

        var options = new SessionCreateOptions
        {
            Mode = "subscription",
            Customer = user.StripeCustomerId,
            ClientReferenceId = user.UserId.ToString(),
            LineItems = items,
            // On the subscription too, so its webhooks find the user even without the customer.
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                Metadata = new Dictionary<string, string> { ["userId"] = user.UserId.ToString() },
            },
            SuccessUrl = $"{WebApp}/profile?subscription=done",
            CancelUrl = $"{WebApp}/profile?subscription=cancelled",
            Locale = LocaleOf(user),
            AutomaticTax = new SessionAutomaticTaxOptions { Enabled = Stripe.AutomaticTax },
            // Stripe Tax needs an address; keep what's entered on the customer.
            CustomerUpdate = Stripe.AutomaticTax ? new SessionCustomerUpdateOptions { Address = "auto", Name = "auto" } : null,
        };
        if (Stripe.RequireTermsConsent)
        {
            options.ConsentCollection = new SessionConsentCollectionOptions { TermsOfService = "required" };
            options.CustomText = new SessionCustomTextOptions
            {
                TermsOfServiceAcceptance = new SessionCustomTextTermsOfServiceAcceptanceOptions
                {
                    Message = user.Language == AppLanguage.Dutch
                        ? "Ik ga akkoord met de gebruiksvoorwaarden en wil dat het abonnement direct ingaat. Daarmee vervalt mijn herroepingsrecht van 14 dagen."
                        : "I agree to the terms of use and want the subscription to start straight away, giving up my 14-day right of withdrawal.",
                },
            };
        }

        return (await _stripe.CreateCheckoutAsync(options, ct), null);
    }

    /// <summary>The Customer Portal's URL; null for someone who never subscribed on the web.</summary>
    public async Task<string?> PortalAsync(Guid userId, CancellationToken ct)
    {
        if (!Available) return null;
        var user = await _db.Users.AsNoTracking().FirstAsync(u => u.UserId == userId, ct);
        return user.StripeCustomerId is null
            ? null
            : await _stripe.CreatePortalAsync(user.StripeCustomerId, $"{WebApp}/profile", LocaleOf(user), ct);
    }

    /// <summary>Reads the subscription from Stripe and brings its rows in step: one per product in it.</summary>
    public async Task SyncAsync(string subscriptionId, CancellationToken ct)
    {
        var subscription = await _stripe.GetSubscriptionAsync(subscriptionId, ct);
        var rows = await _db.Subscriptions
            .Where(s => s.Source == SubscriptionSource.Stripe && s.ExternalId == subscriptionId)
            .ToListAsync(ct);

        if (subscription is null)
        {
            foreach (var row in rows) row.Status = SubscriptionStatus.Expired;
            await _db.SaveChangesAsync(ct);
            return;
        }

        var userId = await UserOfAsync(subscription, ct);
        if (userId is null)
        {
            _logger.LogWarning("Stripe subscription {SubscriptionId} belongs to no known user", subscriptionId);
            return;
        }

        var status = StatusOf(subscription.Status);
        var now = DateTimeOffset.UtcNow;
        var seen = new HashSet<SubscriptionProduct>();
        foreach (var item in subscription.Items?.Data ?? [])
        {
            if (ProductOf(item.Price?.Id) is not { } mapped) continue;
            var (product, period) = mapped;
            seen.Add(product);

            var row = rows.FirstOrDefault(r => r.Product == product);
            // Not yet paid for: nothing to give until it is.
            if (row is null && status is null) continue;
            if (row is null)
            {
                row = new DomainSubscription
                {
                    SubscriptionId = Guid.NewGuid(),
                    UserId = userId.Value,
                    Source = SubscriptionSource.Stripe,
                    ExternalId = subscriptionId,
                    Product = product,
                    CreatedAt = now,
                };
                _db.Subscriptions.Add(row);
            }

            var periodEnd = new DateTimeOffset(DateTime.SpecifyKind(item.CurrentPeriodEnd, DateTimeKind.Utc));
            row.Period = period;
            row.Status = status ?? SubscriptionStatus.OnHold;
            row.CurrentPeriodEnd = periodEnd;
            row.CancelAt = subscription.CancelAt is { } cancelAt
                ? new DateTimeOffset(DateTime.SpecifyKind(cancelAt, DateTimeKind.Utc))
                : subscription.CancelAtPeriodEnd ? periodEnd : null;
            row.UpdatedAt = now;
        }

        // Taken out of the subscription, e.g. Coaching removed in the portal.
        foreach (var row in rows.Where(r => !seen.Contains(r.Product) && r.Status != SubscriptionStatus.Cancelled))
        {
            row.Status = SubscriptionStatus.Cancelled;
            row.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Cancels the user's Stripe subscriptions at once, before their account is deleted, so
    /// nobody pays for an account that's gone. Throws when Stripe can't be reached.
    /// </summary>
    public async Task CancelAllAsync(Guid userId, CancellationToken ct)
    {
        var ids = await _db.Subscriptions
            .Where(s => s.UserId == userId && s.Source == SubscriptionSource.Stripe
                        && s.Status != SubscriptionStatus.Cancelled && s.Status != SubscriptionStatus.Expired)
            .Select(s => s.ExternalId)
            .Distinct()
            .ToListAsync(ct);
        foreach (var id in ids) await _stripe.CancelSubscriptionAsync(id, ct);
    }

    /// <summary>The four prices, read from Stripe and kept for an hour; null while Stripe isn't set up.</summary>
    public async Task<SubscriptionPrices?> PricesAsync(CancellationToken ct)
    {
        if (!Available) return null;
        return await _cache.GetOrCreateAsync("stripe-prices", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            var pm = await _stripe.GetPriceAsync(Stripe.PremiumMonthPrice!, ct);
            var py = await _stripe.GetPriceAsync(Stripe.PremiumYearPrice!, ct);
            var cm = await _stripe.GetPriceAsync(Stripe.CoachingMonthPrice!, ct);
            var cy = await _stripe.GetPriceAsync(Stripe.CoachingYearPrice!, ct);
            return new SubscriptionPrices(pm.Currency, pm.Amount, py.Amount, cm.Amount, cy.Amount);
        });
    }

    private string WebApp => _options.WebAppUrl.TrimEnd('/');

    private string PriceFor(SubscriptionProduct product, SubscriptionPeriod period) => (product, period) switch
    {
        (SubscriptionProduct.Premium, SubscriptionPeriod.Month) => Stripe.PremiumMonthPrice!,
        (SubscriptionProduct.Premium, SubscriptionPeriod.Year) => Stripe.PremiumYearPrice!,
        (SubscriptionProduct.Coaching, SubscriptionPeriod.Month) => Stripe.CoachingMonthPrice!,
        _ => Stripe.CoachingYearPrice!,
    };

    private (SubscriptionProduct, SubscriptionPeriod)? ProductOf(string? priceId) => priceId switch
    {
        _ when priceId == Stripe.PremiumMonthPrice => (SubscriptionProduct.Premium, SubscriptionPeriod.Month),
        _ when priceId == Stripe.PremiumYearPrice => (SubscriptionProduct.Premium, SubscriptionPeriod.Year),
        _ when priceId == Stripe.CoachingMonthPrice => (SubscriptionProduct.Coaching, SubscriptionPeriod.Month),
        _ when priceId == Stripe.CoachingYearPrice => (SubscriptionProduct.Coaching, SubscriptionPeriod.Year),
        _ => null,
    };

    /// <summary>Stripe's status, simplified; null for "incomplete": the first payment hasn't gone through yet.</summary>
    internal static SubscriptionStatus? StatusOf(string status) => status switch
    {
        "active" or "trialing" => SubscriptionStatus.Active,
        "past_due" => SubscriptionStatus.InGracePeriod,
        "unpaid" or "paused" => SubscriptionStatus.OnHold,
        "canceled" => SubscriptionStatus.Cancelled,
        "incomplete_expired" => SubscriptionStatus.Expired,
        _ => null,
    };

    private async Task<Guid?> UserOfAsync(Stripe.Subscription subscription, CancellationToken ct)
    {
        if (subscription.Metadata?.TryGetValue("userId", out var id) == true && Guid.TryParse(id, out var fromMetadata)
            && await _db.Users.AnyAsync(u => u.UserId == fromMetadata, ct))
            return fromMetadata;
        return await _db.Users.Where(u => u.StripeCustomerId == subscription.CustomerId)
            .Select(u => (Guid?)u.UserId).FirstOrDefaultAsync(ct);
    }

    private static string? LocaleOf(User user) => user.Language == AppLanguage.Dutch ? "nl" : "auto";
}
