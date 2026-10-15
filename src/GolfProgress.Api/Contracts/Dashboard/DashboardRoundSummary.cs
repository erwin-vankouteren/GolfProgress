using GolfProgress.Api.OpenApi;
using Enums = GolfProgress.Domain.Enums;
namespace GolfProgress.Api.Contracts.Dashboard;

public sealed class DashboardRoundSummary
{
    public Guid RoundId { get; init; }
    public string PlayedAt { get; init; } = "";
    public short TotalStrokes { get; init; }

    public short? FitnessScore { get; init; }
    [StringEnum(typeof(Enums.ConfidenceLevel))]
    public string? FitnessConfidence { get; init; }
}