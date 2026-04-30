using GolfProgress.Api.Contracts.MicroWins;
using GolfProgress.Api.Contracts.Recommendations;

namespace GolfProgress.Api.Contracts.Dashboard;

public sealed class DashboardResponse
{
    public Guid UserId { get; init; }

    public DashboardRoundSummary? LastRound { get; init; }

    public List<MicroWinResponse> MicroWins { get; init; } = new();

    public NextLeverResponse? NextLever { get; init; }

    public decimal? BaselineAvgScore { get; init; }
    public string? BaselineConfidence { get; init; }

    public decimal? MonthlyFitnessAvg { get; init; }
    public decimal? MonthlyFitnessChange { get; init; }

    public DateTimeOffset GeneratedAt { get; init; }
}