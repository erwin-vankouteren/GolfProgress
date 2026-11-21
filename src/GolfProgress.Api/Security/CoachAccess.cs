using GolfProgress.Api.Services;
using GolfProgress.Domain.Entities;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GolfProgress.Api.Security;

/// <summary>What a coach can see of a golfer (docs/coach-access.md → "What a coach sees").</summary>
public enum CoachShare
{
    /// <summary>Rounds, scores, next focus, baseline, timeline and reviews: always shared.</summary>
    RoundsAndProgress,

    /// <summary>Round reflections, and the free-text notes on rounds and practice sessions.</summary>
    Reflections,

    /// <summary>Practice sessions and practice goals.</summary>
    Practice,
}

/// <summary>
/// The one check behind every coach request (#194): the caller has an active link to the
/// golfer, and its sharing covers what's asked for. Read on every request, so removing a
/// coach or switching sharing off applies to the next one. Callers answer 404 when it
/// fails, so a coach can't tell whether a golfer or item exists.
/// A link is also paused while the golfer has no Coaching, in the trial or as an add-on
/// (#312, docs/payments.md): it counts as not active until they have it again.
/// </summary>
public sealed class CoachAccess
{
    private readonly GolfProgressDbContext _db;
    private readonly PlanService _plans;

    public CoachAccess(GolfProgressDbContext db, PlanService plans)
    {
        _db = db;
        _plans = plans;
    }

    /// <summary>The caller's active link to the golfer, with the golfer; null without one, or while it's paused.</summary>
    public async Task<CoachLink?> ActiveLinkAsync(Guid coachId, Guid golferId, CancellationToken ct)
    {
        var link = await _db.CoachLinks.AsNoTracking().Include(l => l.Golfer)
            .FirstOrDefaultAsync(l => l.CoachId == coachId && l.GolferId == golferId && l.EndedAt == null, ct);
        return link is not null && await _plans.HasCoachingAsync(golferId, ct) ? link : null;
    }

    /// <summary>The caller's active link to the golfer when it covers [share]; null otherwise.</summary>
    public async Task<CoachLink?> LinkCoveringAsync(Guid coachId, Guid golferId, CoachShare share, CancellationToken ct) =>
        await ActiveLinkAsync(coachId, golferId, ct) is { } link && Covers(link, share) ? link : null;

    public static bool Covers(CoachLink link, CoachShare share) => share switch
    {
        CoachShare.RoundsAndProgress => true,
        CoachShare.Reflections => link.ShareReflections,
        CoachShare.Practice => link.SharePractice,
        _ => false,
    };
}
