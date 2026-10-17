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
        _db.Users.Remove(user);

        // One save, so one transaction: the account is deleted entirely or not at all.
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
