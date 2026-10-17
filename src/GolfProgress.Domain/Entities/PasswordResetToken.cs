namespace GolfProgress.Domain.Entities;

/// <summary>
/// A password reset link's token (#86). Single use and short-lived; only its hash is
/// stored, as for refresh tokens, so a database leak doesn't hand out reset links.
/// </summary>
public sealed class PasswordResetToken
{
    public Guid PasswordResetTokenId { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public string TokenHash { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>When the link was used, or made void by a later reset.</summary>
    public DateTimeOffset? UsedAt { get; set; }
}
