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
/// </summary>
public sealed class CoachAccess
{
    private readonly GolfProgressDbContext _db;

    public CoachAccess(GolfProgressDbContext db) => _db = db;

    /// <summary>The caller's active link to the golfer, with the golfer; null without one.</summary>
    public Task<CoachLink?> ActiveLinkAsync(Guid coachId, Guid golferId, CancellationToken ct) =>
        _db.CoachLinks.AsNoTracking().Include(l => l.Golfer)
            .FirstOrDefaultAsync(l => l.CoachId == coachId && l.GolferId == golferId && l.EndedAt == null, ct);

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
