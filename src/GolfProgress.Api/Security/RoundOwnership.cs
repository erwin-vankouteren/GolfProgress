using GolfProgress.Domain.Entities;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GolfProgress.Api.Security;

/// <summary>
/// Round lookups scoped to the calling user. Every endpoint keyed on a round id
/// must go through one of these so a valid token for user A cannot reach user B's data.
/// </summary>
public static class RoundOwnership
{
    /// <summary>
    /// Loads a tracked round only when it belongs to <paramref name="userId"/>.
    /// Returns null both when the round is missing and when it belongs to someone
    /// else, so callers answer 404 without revealing which case it was.
    /// </summary>
    public static Task<Round?> FindOwnedRoundAsync(
        this GolfProgressDbContext db, Guid roundId, Guid userId, CancellationToken ct)
        => db.Rounds.FirstOrDefaultAsync(r => r.RoundId == roundId && r.UserId == userId, ct);

    /// <summary>As <see cref="FindOwnedRoundAsync"/>, without change tracking.</summary>
    public static Task<Round?> FindOwnedRoundReadOnlyAsync(
        this GolfProgressDbContext db, Guid roundId, Guid userId, CancellationToken ct)
        => db.Rounds.AsNoTracking()
             .FirstOrDefaultAsync(r => r.RoundId == roundId && r.UserId == userId, ct);

    /// <summary>Ownership probe for paths that do not need the round entity itself.</summary>
    public static Task<bool> OwnsRoundAsync(
        this GolfProgressDbContext db, Guid roundId, Guid userId, CancellationToken ct)
        => db.Rounds.AnyAsync(r => r.RoundId == roundId && r.UserId == userId, ct);
}
