namespace GolfProgress.Api.Contracts.Coaching;

/// <summary>A pending invitation, as the golfer who sent it sees it (#184).</summary>
public sealed class CoachInvitationResponse
{
    public Guid CoachInvitationId { get; init; }
    public string Email { get; init; } = "";
    public bool ShareReflections { get; init; }
    public bool SharePractice { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }

    /// <summary>True when this was an open invitation to the same address, sent again with a new link.</summary>
    public bool Resent { get; init; }
}
