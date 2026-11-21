using System.ComponentModel.DataAnnotations;

namespace GolfProgress.Api.Configuration;

/// <summary>Subscriptions (#309, #310, docs/payments.md).</summary>
public sealed class PaymentsOptions
{
    public const string SectionName = "Payments";

    /// <summary>
    /// When subscriptions start. Until it's set, everyone uses the app free, as in the
    /// launch period the terms describe. From then on, a trial ends 30 days after signing
    /// up, but never earlier than <see cref="TrialDays"/> after this date, so accounts that
    /// existed before get the terms' 30 days' notice.
    /// </summary>
    public DateTimeOffset? LaunchDate { get; set; }

    /// <summary>How long the free trial lasts, and how much notice existing accounts get.</summary>
    [Range(1, 365)]
    public int TrialDays { get; set; } = 30;

    /// <summary>The web app, where Stripe sends people back after checkout and the portal.</summary>
    [Required]
    public string WebAppUrl { get; set; } = "http://localhost:5173";

    public StripeOptions Stripe { get; set; } = new();
}

/// <summary>
/// Stripe, for subscribing on the web (#310). Test-mode keys on staging, live ones in
/// production; the secrets come from Key Vault. Subscribing on the web is off until the
/// secret key and all four prices are set.
/// </summary>
public sealed class StripeOptions
{
    /// <summary>The secret API key: sk_test_… or sk_live_….</summary>
    public string? SecretKey { get; set; }

    /// <summary>The webhook endpoint's signing secret, whsec_…, to check each event comes from Stripe.</summary>
    public string? WebhookSecret { get; set; }

    /// <summary>The price ids, price_…, of the subscription and the Coaching add-on, per period.</summary>
    public string? PremiumMonthPrice { get; set; }
    public string? PremiumYearPrice { get; set; }
    public string? CoachingMonthPrice { get; set; }
    public string? CoachingYearPrice { get; set; }

    /// <summary>
    /// Stripe Tax adds VAT at checkout; it has to be switched on in the Stripe dashboard. Off
    /// (the default): the prices are what customers pay, VAT included, accounted for outside Stripe.
    /// </summary>
    public bool AutomaticTax { get; set; }

    /// <summary>
    /// Checkout asks to accept the terms, and with them to start straight away, giving up
    /// the 14-day right of withdrawal. Needs the terms' URL in Stripe → Settings → Public details.
    /// </summary>
    public bool RequireTermsConsent { get; set; } = true;

    public bool Configured =>
        !string.IsNullOrWhiteSpace(SecretKey) &&
        new[] { PremiumMonthPrice, PremiumYearPrice, CoachingMonthPrice, CoachingYearPrice }.All(p => !string.IsNullOrWhiteSpace(p));
}
