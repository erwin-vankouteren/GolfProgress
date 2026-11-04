namespace GolfProgress.Api.Contracts.Coaching;

/// <summary>The coach's new link to the golfer who invited them (#184).</summary>
public sealed class AcceptedCoachInvitationResponse
{
    public Guid CoachLinkId { get; init; }
    public Guid GolferId { get; init; }

    /// <summary>The golfer's display name; coaches never see a golfer's email address.</summary>
    public string GolferDisplayName { get; init; } = "";

    public bool ShareReflections { get; init; }
    public bool SharePractice { get; init; }
}
