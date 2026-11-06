namespace GolfProgress.Api.Contracts.Coaching;

/// <summary>A golfer's active coaches and open invitations (#185, #186).</summary>
public sealed class CoachesResponse
{
    public IReadOnlyList<CoachResponse> Coaches { get; init; } = [];
    public IReadOnlyList<CoachInvitationResponse> Invitations { get; init; } = [];
}

/// <summary>An active coach, and what they see.</summary>
public sealed class CoachResponse
{
    public Guid CoachLinkId { get; init; }
    public string DisplayName { get; init; } = "";
    public bool ShareReflections { get; init; }
    public bool SharePractice { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
