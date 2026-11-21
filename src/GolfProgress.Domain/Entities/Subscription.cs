using GolfProgress.Domain.Enums;

namespace GolfProgress.Domain.Entities;

/// <summary>
/// One paid product a user has from Stripe or Google Play (docs/payments.md → "Data model").
/// The payment provider's webhooks keep it up to date; whether the user is subscribed is
/// worked out from these rows when it's read. A Stripe subscription with Coaching added is
/// two rows with the same <see cref="ExternalId"/>: one per product.
/// </summary>
public sealed class Subscription
{
    public Guid SubscriptionId { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public SubscriptionSource Source { get; set; }

    /// <summary>The Stripe subscription id, or the Google Play purchase token.</summary>
    public string ExternalId { get; set; } = "";

    public SubscriptionProduct Product { get; set; }
    public SubscriptionPeriod Period { get; set; }
    public SubscriptionStatus Status { get; set; }

    /// <summary>When the period that's paid for ends: the renewal date, unless it's cancelled.</summary>
    public DateTimeOffset CurrentPeriodEnd { get; set; }

    /// <summary>Set when the user cancelled: access lasts until then, and it won't renew.</summary>
    public DateTimeOffset? CancelAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Whether this row gives access now: active, or the store is still retrying a payment.</summary>
    public bool GivesAccess => Status is SubscriptionStatus.Active or SubscriptionStatus.InGracePeriod;
}
