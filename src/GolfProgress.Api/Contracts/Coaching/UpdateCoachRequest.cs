namespace GolfProgress.Api.Contracts.Coaching;

/// <summary>Change what a coach sees (#189). What's left out stays as it is.</summary>
public sealed class UpdateCoachRequest
{
    public bool? ShareReflections { get; init; }
    public bool? SharePractice { get; init; }
}
