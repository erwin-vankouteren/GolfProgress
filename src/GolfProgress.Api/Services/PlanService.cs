using GolfProgress.Api.Configuration;
using GolfProgress.Domain.Entities;
using GolfProgress.Domain.Enums;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GolfProgress.Api.Services;

/// <summary>What a user can do (docs/payments.md → "What each state allows").</summary>
public enum Plan
{
    /// <summary>The free trial, or the launch period before subscriptions start: everything.</summary>
    Trial,

    /// <summary>A subscription from Stripe or Google Play.</summary>
    Subscribed,

    /// <summary>The trial ended without a subscription: everything can be read, nothing logged.</summary>
    ReadOnly,
}

/// <summary>A user's plan, as GET /me reports it.</summary>
/// <param name="TrialEndsAt">Null in the launch period, before subscriptions start.</param>
/// <param name="Coaching">In the trial, or with the Coaching add-on.</param>
/// <param name="Subscription">The subscription that gives access, if any.</param>
public sealed record PlanState(Plan Plan, DateTimeOffset? TrialEndsAt, bool Coaching, Subscription? Subscription, Subscription? CoachingAddOn)
{
    /// <summary>Rounds, reflections, practice and reviews can be logged and changed.</summary>
    public bool CanLog => Plan != Plan.ReadOnly;
}

/// <summary>
/// Works out a user's plan from when they signed up, the launch date and their
/// subscriptions (#309, docs/payments.md). Nothing about it is stored: webhooks keep the
/// subscriptions up to date, and the plan follows from them on every read.
/// </summary>
public sealed class PlanService
{
    private readonly GolfProgressDbContext _db;
    private readonly PaymentsOptions _options;
    public PlanService(GolfProgressDbContext db, IOptions<PaymentsOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<PlanState> ForAsync(User user, CancellationToken ct) =>
        Compute(user.CreatedAt, await SubscriptionsOfAsync(user.UserId, ct), _options, DateTimeOffset.UtcNow);

    /// <summary>The plan of the user with this id; null when there's no such user.</summary>
    public async Task<PlanState?> ForUserAsync(Guid userId, CancellationToken ct)
    {
        var createdAt = await _db.Users.AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => (DateTimeOffset?)u.CreatedAt)
            .FirstOrDefaultAsync(ct);
        return createdAt is { } signedUp
            ? Compute(signedUp, await SubscriptionsOfAsync(userId, ct), _options, DateTimeOffset.UtcNow)
            : null;
    }

    private Task<List<Subscription>> SubscriptionsOfAsync(Guid userId, CancellationToken ct) =>
        _db.Subscriptions.AsNoTracking().Where(s => s.UserId == userId).ToListAsync(ct);

    /// <summary>The rules, apart from the database, so each case can be tested on its own.</summary>
    public static PlanState Compute(
        DateTimeOffset signedUpAt,
        IReadOnlyCollection<Subscription> subscriptions,
        PaymentsOptions options,
        DateTimeOffset now)
    {
        var premium = Giving(subscriptions, SubscriptionProduct.Premium);
        var coaching = Giving(subscriptions, SubscriptionProduct.Coaching);

        if (options.LaunchDate is not { } launch)
            return new PlanState(premium is null ? Plan.Trial : Plan.Subscribed, null, Coaching: true, premium, coaching);

        var trial = TimeSpan.FromDays(options.TrialDays);
        var trialEndsAt = Max(signedUpAt + trial, launch + trial);
        var inTrial = now < trialEndsAt;

        var plan = premium is not null ? Plan.Subscribed : inTrial ? Plan.Trial : Plan.ReadOnly;
        // The add-on only counts alongside the subscription: on its own it gives nothing.
        var hasCoaching = inTrial || (premium is not null && coaching is not null);
        return new PlanState(plan, trialEndsAt, hasCoaching, premium, coaching);
    }

    /// <summary>The row for [product] that gives access now, preferring the one that lasts longest.</summary>
    private static Subscription? Giving(IEnumerable<Subscription> subscriptions, SubscriptionProduct product) =>
        subscriptions
            .Where(s => s.Product == product && s.GivesAccess)
            .OrderByDescending(s => s.CurrentPeriodEnd)
            .FirstOrDefault();

    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;
}
