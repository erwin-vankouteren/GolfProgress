using GolfProgress.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GolfProgress.Api.Services;

/// <summary>
/// Deletes a user's account and everything that belongs to it (#84). The App Store
/// and Google Play require apps that create accounts to offer this in the app.
///
/// What is deleted is listed in docs/architecture.md, "Deleting an account", which the
/// privacy policy relies on: keep the two in step.
/// </summary>
public sealed class AccountDeletionService
{
    private readonly GolfProgressDbContext _db;

    public AccountDeletionService(GolfProgressDbContext db) => _db = db;

    /// <summary>
    /// Deletes the user and every row that references them. Returns false when there is
    /// no such user.
    /// </summary>
    public async Task<bool> DeleteAsync(Guid userId, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId, ct);
        if (user is null) return false;

        // Rounds, reflections, fitness scores and micro-wins reference Users with
        // Restrict, so the database won't cascade the user's deletion to them; the rest
        // cascade. Every row is loaded and removed here regardless, so nothing depends
        // on which do: EF orders the deletes so that children go before their parents.
        _db.MicroWins.RemoveRange(await _db.MicroWins.Where(x => x.UserId == userId).ToListAsync(ct));
        _db.FitnessScores.RemoveRange(await _db.FitnessScores.Where(x => x.UserId == userId).ToListAsync(ct));
        _db.Reflections.RemoveRange(await _db.Reflections.Where(x => x.UserId == userId).ToListAsync(ct));
        _db.Rounds.RemoveRange(await _db.Rounds.Where(x => x.UserId == userId).ToListAsync(ct));
        _db.PracticeSessions.RemoveRange(await _db.PracticeSessions.Where(x => x.UserId == userId).ToListAsync(ct));
        _db.MonthlyReviews.RemoveRange(await _db.MonthlyReviews.Where(x => x.UserId == userId).ToListAsync(ct));
        _db.UserBaselines.RemoveRange(await _db.UserBaselines.Where(x => x.UserId == userId).ToListAsync(ct));
        _db.Events.RemoveRange(await _db.Events.Where(x => x.UserId == userId).ToListAsync(ct));
        // Deleting the refresh tokens is what revokes them: every device is signed out
        // at its next refresh.
        _db.RefreshTokens.RemoveRange(await _db.RefreshTokens.Where(x => x.UserId == userId).ToListAsync(ct));
        _db.PasswordResetTokens.RemoveRange(await _db.PasswordResetTokens.Where(x => x.UserId == userId).ToListAsync(ct));
        // Coaching (#184): the user's invitations, and their links as golfer or as coach.
        // Links don't cascade from either end, so both sides are removed here.
        // Invitations sent to the user's address go too: they hold it.
        var address = user.Email?.Trim().ToLowerInvariant();
        _db.CoachInvitations.RemoveRange(await _db.CoachInvitations
            .Where(x => x.GolferId == userId || (address != null && x.Email == address)).ToListAsync(ct));
        _db.CoachLinks.RemoveRange(await _db.CoachLinks.Where(x => x.GolferId == userId || x.CoachId == userId).ToListAsync(ct));
        // Feedback (#212): a golfer's threads go with them. What a coach wrote stays with the
        // golfers it was written for, without the author: "a former coach".
        var threads = await _db.FeedbackMessages.Where(x => x.GolferId == userId).ToListAsync(ct);
        _db.FeedbackMessages.RemoveRange(threads);
        // A coach's "FeedbackWritten" events about those messages hold the golfer's id in
        // their payload: they go too, so nothing in the coach's log points at the golfer.
        var threadIds = threads.Select(x => x.FeedbackMessageId).Cast<Guid?>().ToList();
        _db.Events.RemoveRange(await _db.Events
            .Where(x => x.EventType == "FeedbackWritten" && threadIds.Contains(x.RelatedId)).ToListAsync(ct));
        foreach (var message in await _db.FeedbackMessages
                     .Where(x => x.GolferId != userId && (x.CoachId == userId || x.AuthorId == userId)).ToListAsync(ct))
        {
            if (message.CoachId == userId) message.CoachId = null;
            if (message.AuthorId == userId) message.AuthorId = null;
        }
        _db.FeedbackReads.RemoveRange(await _db.FeedbackReads.Where(x => x.UserId == userId || x.GolferId == userId).ToListAsync(ct));
        // Practice goals (#226): a golfer's go with them; those a coach set stay with the
        // golfer, "from a former coach".
        _db.PracticeGoals.RemoveRange(await _db.PracticeGoals.Where(x => x.GolferId == userId).ToListAsync(ct));
        foreach (var goal in await _db.PracticeGoals.Where(x => x.GolferId != userId && x.CoachId == userId).ToListAsync(ct))
            goal.CoachId = null;
        _db.Users.Remove(user);

        // One save, so one transaction: the account is deleted entirely or not at all.
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
