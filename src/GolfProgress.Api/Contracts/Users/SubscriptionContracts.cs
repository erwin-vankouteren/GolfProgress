using System.ComponentModel.DataAnnotations;
using GolfProgress.Api.OpenApi;
using Enums = GolfProgress.Domain.Enums;

namespace GolfProgress.Api.Contracts.Users;

/// <summary>What to subscribe to on the web (#310): the period, and whether Coaching comes with it.</summary>
public sealed class CheckoutRequest
{
    [Required, StringEnum(typeof(Enums.SubscriptionPeriod))]
    public string Period { get; init; } = "";

    /// <summary>Add the Coaching add-on.</summary>
    public bool Coaching { get; init; }
}

/// <summary>A Stripe page to send the user to: checkout or the Customer Portal.</summary>
public sealed class RedirectResponse
{
    public string Url { get; init; } = "";
}

/// <summary>The prices on the web, in cents including VAT, as Stripe has them.</summary>
public sealed class SubscriptionPricesResponse
{
    /// <summary>Lowercase ISO code, e.g. "eur".</summary>
    public string Currency { get; init; } = "";
    public long PremiumMonth { get; init; }
    public long PremiumYear { get; init; }
    public long CoachingMonth { get; init; }
    public long CoachingYear { get; init; }
}
