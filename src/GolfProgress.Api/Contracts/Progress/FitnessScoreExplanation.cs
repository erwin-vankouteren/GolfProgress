namespace GolfProgress.Api.Contracts.Progress;

/// <summary>
/// What a fitness score was judged on (#95), so the app can explain each pillar. Read
/// from what the score stored when it was computed; a field is null when the score
/// predates it or there was nothing to judge against.
/// </summary>
public sealed class FitnessScoreExplanation
{
    /// <summary>
    /// The average of the rounds played before this one, in strokes per 18 holes: what
    /// Execution and Consistency compared the round with. Null for a first round.
    /// </summary>
    public decimal? BaselineAvgScore { get; init; }

    /// <summary>How much those rounds varied (standard deviation, per 18 holes).</summary>
    public decimal? BaselineStdDev { get; init; }

    /// <summary>Rounds played in the 28 days up to this one, this one included (Habits).</summary>
    public int? RoundsInHabitWindow { get; init; }

    /// <summary>Practice sessions in the same 28 days (Habits).</summary>
    public int? PracticeSessionsInHabitWindow { get; init; }

    /// <summary>Whether the round had a reflection when it was scored (Habits).</summary>
    public bool? Reflected { get; init; }
}
