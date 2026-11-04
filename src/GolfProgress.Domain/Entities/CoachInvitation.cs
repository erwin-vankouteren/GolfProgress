namespace GolfProgress.Domain.Entities;

/// <summary>
/// A golfer's invitation to a coach (#184, docs/coach-access.md → "Inviting a coach").
/// The coach accepts it through a single-use link valid for 7 days; only a hash of its
/// token is stored, as for password reset links.
/// </summary>
public sealed class CoachInvitation
{
    public Guid CoachInvitationId { get; set; }

    public Guid GolferId { get; set; }
    public User Golfer { get; set; } = default!;

    /// <summary>The coach's address, lower-cased: the account that accepts must have it.</summary>
    public string Email { get; set; } = "";

    public string TokenHash { get; set; } = "";

    public bool ShareReflections { get; set; }
    public bool SharePractice { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? AcceptedAt { get; set; }
    public DateTimeOffset? DeclinedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }

    /// <summary>Not yet accepted, declined or cancelled; it may still have expired.</summary>
    public bool IsOpen => AcceptedAt is null && DeclinedAt is null && CancelledAt is null;
}
