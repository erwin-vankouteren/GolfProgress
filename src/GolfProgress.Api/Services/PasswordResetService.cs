using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using GolfProgress.Api.Email;
using GolfProgress.Domain.Entities;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GolfProgress.Api.Services;

/// <summary>Password reset settings (#86); see docs/deployment.md → "Email".</summary>
public sealed class PasswordResetOptions
{
    public const string SectionName = "PasswordReset";

    /// <summary>How long a reset link works.</summary>
    [Range(1, 1440)]
    public int TokenLifetimeMinutes { get; set; } = 30;

    /// <summary>Reset emails one account can be sent per hour, so the feature can't be used to flood an inbox.</summary>
    [Range(1, 100)]
    public int MaxPerHour { get; set; } = 3;

    /// <summary>The web app's reset page; the token is added as ?token=.</summary>
    [Required, Url]
    public string LinkBaseUrl { get; set; } = "http://localhost:5173/reset-password";
}

/// <summary>
/// Password reset by email (#86). Asking always looks the same from outside, whether or
/// not the account exists; the link is single use and short-lived; completing it
/// revokes every session, so anyone holding the old password is signed out.
/// </summary>
public sealed class PasswordResetService
{
    private readonly GolfProgressDbContext _db;
    private readonly IEmailOutbox _outbox;
    private readonly PasswordResetOptions _options;
    private readonly ILogger<PasswordResetService> _log;

    public PasswordResetService(
        GolfProgressDbContext db,
        IEmailOutbox outbox,
        IOptions<PasswordResetOptions> options,
        ILogger<PasswordResetService> log)
    {
        _db = db;
        _outbox = outbox;
        _options = options.Value;
        _log = log;
    }

    /// <summary>Sends a reset link to [email] if it belongs to an account and the hourly limit allows.</summary>
    public async Task RequestAsync(string email, CancellationToken ct)
    {
        var normalised = email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == normalised, ct);
        if (user?.Email is null) return;

        var now = DateTimeOffset.UtcNow;
        var recent = await _db.PasswordResetTokens.CountAsync(t => t.UserId == user.UserId && t.CreatedAt > now.AddHours(-1), ct);
        if (recent >= _options.MaxPerHour)
        {
            _log.LogInformation("Password reset for account {UserId} not sent: over the hourly limit", user.UserId);
            return;
        }

        var token = Base64Url(RandomNumberGenerator.GetBytes(32));
        _db.PasswordResetTokens.Add(new PasswordResetToken
        {
            PasswordResetTokenId = Guid.NewGuid(),
            UserId = user.UserId,
            TokenHash = Hash(token),
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(_options.TokenLifetimeMinutes),
        });
        await _db.SaveChangesAsync(ct);

        _outbox.Enqueue(ResetEmail(user.Email, LinkFor(token)));
    }

    /// <summary>The account a link belongs to, or null when it's unknown, used or expired.</summary>
    public async Task<User?> FindUserAsync(string token, CancellationToken ct)
    {
        var hash = Hash(token);
        var now = DateTimeOffset.UtcNow;
        var reset = await _db.PasswordResetTokens.Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.UsedAt == null && t.ExpiresAt > now, ct);
        return reset?.User;
    }

    /// <summary>
    /// Sets the new password (already checked against the policy): every outstanding link
    /// for the account becomes void, and every session is revoked.
    /// </summary>
    public async Task CompleteAsync(User user, string newPasswordHash, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        user.PasswordHash = newPasswordHash;
        user.UpdatedAt = now;

        foreach (var link in await _db.PasswordResetTokens.Where(t => t.UserId == user.UserId && t.UsedAt == null).ToListAsync(ct))
            link.UsedAt = now;

        foreach (var session in await _db.RefreshTokens.Where(t => t.UserId == user.UserId && t.RevokedAt == null).ToListAsync(ct))
        {
            session.RevokedAt = now;
            session.ReasonRevoked = "PasswordReset";
        }

        // One save: the password, the used links and the revoked sessions together.
        await _db.SaveChangesAsync(ct);
    }

    internal string LinkFor(string token) =>
        $"{_options.LinkBaseUrl}{(_options.LinkBaseUrl.Contains('?') ? '&' : '?')}token={Uri.EscapeDataString(token)}";

    private EmailMessage ResetEmail(string to, string link)
    {
        var minutes = _options.TokenLifetimeMinutes;
        return new EmailMessage(
            to,
            "Reset your GolfProgress password",
            $"Someone asked to reset the password for your GolfProgress account.\n\n" +
            $"To choose a new password, open this link within {minutes} minutes:\n{link}\n\n" +
            "If you didn't ask, ignore this email: your password stays as it is.",
            $"<p>Someone asked to reset the password for your GolfProgress account.</p>" +
            $"<p>To choose a new password, open this link within {minutes} minutes:<br>" +
            $"<a href=\"{System.Net.WebUtility.HtmlEncode(link)}\">Reset your password</a></p>" +
            "<p>If you didn't ask, ignore this email: your password stays as it is.</p>");
    }

    internal static string Hash(string token) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
