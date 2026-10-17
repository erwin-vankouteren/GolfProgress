using GolfProgress.Api.OpenApi;
using Enums = GolfProgress.Domain.Enums;
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
    [StringEnum(typeof(Enums.ConfidenceLevel))]
    public string? BaselineConfidence { get; init; }

    /// <summary>
    /// The average fitness score of this calendar month's rounds (UTC), or null when no
    /// round this month is scored.
    /// </summary>
    public decimal? MonthlyFitnessAvg { get; init; }

    /// <summary>This month's average minus last month's; null unless both have one.</summary>
    public decimal? MonthlyFitnessChange { get; init; }

    public DateTimeOffset GeneratedAt { get; init; }
}