namespace GolfProgress.Domain.Enums;

/// <summary>Where a subscription was bought (docs/payments.md).</summary>
public enum SubscriptionSource { Stripe, GooglePlay }

/// <summary>What's paid for: the subscription itself, or the Coaching add-on.</summary>
public enum SubscriptionProduct { Premium, Coaching }

public enum SubscriptionPeriod { Month, Year }

/// <summary>
/// The provider's state, simplified. Active and InGracePeriod give access; OnHold (a
/// payment failed and the store stopped retrying), Cancelled and Expired don't.
/// </summary>
public enum SubscriptionStatus { Active, InGracePeriod, OnHold, Cancelled, Expired }
