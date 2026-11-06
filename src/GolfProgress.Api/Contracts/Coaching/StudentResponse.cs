namespace GolfProgress.Api.Contracts.Coaching;

/// <summary>A golfer the caller coaches (#199). Never their email address.</summary>
public sealed class StudentResponse
{
    public Guid GolferId { get; init; }
    public string DisplayName { get; init; } = "";

    /// <summary>The date of their latest round, or practice session if practice is shared; null before the first.</summary>
    public DateOnly? LastActivityOn { get; init; }

    public int UnreadReplies { get; init; }
    public bool ShareReflections { get; init; }
    public bool SharePractice { get; init; }
}
