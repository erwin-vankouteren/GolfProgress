namespace GolfProgress.Api.Contracts.Coaching;

/// <summary>What an invitation offers, for the invited coach to decide on (#185).</summary>
public sealed class CoachInvitationPreviewResponse
{
    /// <summary>The golfer's display name; coaches never see a golfer's email address.</summary>
    public string GolferDisplayName { get; init; } = "";

    public bool ShareReflections { get; init; }
    public bool SharePractice { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
}
