namespace GolfProgress.Api.Contracts.Timeline;

public sealed class MonthlyTimelinePointResponse
{
    public string MonthStart { get; init; } = ""; // yyyy-MM-dd

    public decimal? FitnessAvg { get; init; }
    public decimal? FitnessChange { get; init; }

    /// <summary>Every round played in the month, scored or not.</summary>
    public int RoundsLogged { get; init; }

    public List<string> Highlights { get; init; } = new();

    /// <summary>Whether a monthly review was generated for this month (#107).</summary>
    public bool HasReview { get; init; }
}