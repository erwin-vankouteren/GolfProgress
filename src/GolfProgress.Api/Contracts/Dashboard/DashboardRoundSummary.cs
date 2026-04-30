namespace GolfProgress.Api.Contracts.Dashboard;

public sealed class DashboardRoundSummary
{
    public Guid RoundId { get; init; }
    public string PlayedAt { get; init; } = "";
    public short TotalStrokes { get; init; }

    public short? FitnessScore { get; init; }
    public string? FitnessConfidence { get; init; }
}