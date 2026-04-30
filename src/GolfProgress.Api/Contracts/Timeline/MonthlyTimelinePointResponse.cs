namespace GolfProgress.Api.Contracts.Timeline;

public sealed class MonthlyTimelinePointResponse
{
    public string MonthStart { get; init; } = ""; // yyyy-MM-dd

    public decimal? FitnessAvg { get; init; }
    public decimal? FitnessChange { get; init; }

    public int RoundsLogged { get; init; }

    public List<string> Highlights { get; init; } = new();
}