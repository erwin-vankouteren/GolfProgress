namespace GolfProgress.Domain.Entities;

public sealed class RefreshToken
{
    public Guid RefreshTokenId { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    // Store only the hash of the refresh token (never the raw token)
    public string TokenHash { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    // For rotation / audit
    public string? ReplacedByTokenHash { get; set; }
    public string? ReasonRevoked { get; set; }
}