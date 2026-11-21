using GolfProgress.Api.OpenApi;
using GolfProgress.Api.Services;
using Enums = GolfProgress.Domain.Enums;

namespace GolfProgress.Api.Contracts.Users;

/// <summary>A user's subscription, on GET /me (#309, docs/payments.md).</summary>
public sealed class SubscriptionResponse
{
    /// <summary>Where it was bought, and so where it's managed: Stripe on the web, or Google Play.</summary>
    [StringEnum(typeof(Enums.SubscriptionSource))]
    public string Source { get; init; } = "";

    [StringEnum(typeof(Enums.SubscriptionPeriod))]
    public string Period { get; init; } = "";

    /// <summary>When it renews next; null once it's cancelled.</summary>
    public DateTimeOffset? RenewsAt { get; init; }

    /// <summary>When it ends after being cancelled; null while it renews.</summary>
    public DateTimeOffset? CancelsAt { get; init; }

    /// <summary>Whether the Coaching add-on is part of it.</summary>
    public bool CoachingAddOn { get; init; }

    public static SubscriptionResponse? From(PlanState plan) =>
        plan.Subscription is not { } premium
            ? null
            : new SubscriptionResponse
            {
                Source = premium.Source.ToString(),
                Period = premium.Period.ToString(),
                RenewsAt = premium.CancelAt is null ? premium.CurrentPeriodEnd : null,
                CancelsAt = premium.CancelAt,
                CoachingAddOn = plan.CoachingAddOn is not null,
            };
}
