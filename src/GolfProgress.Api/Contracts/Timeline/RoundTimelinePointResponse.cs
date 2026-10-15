using GolfProgress.Api.OpenApi;
using Enums = GolfProgress.Domain.Enums;
namespace GolfProgress.Api.Contracts.Timeline;

public sealed class RoundTimelinePointResponse
{
    public Guid RoundId { get; init; }
    public string PlayedAt { get; init; } = ""; // yyyy-MM-dd

    public short TotalStrokes { get; init; }
    public short? TotalPutts { get; init; }
    public short? Penalties { get; init; }
    public short? ThreePutts { get; init; }
    public short? BlowUpHoles { get; init; }

    public short? FitnessScoreTotal { get; init; }
    [StringEnum(typeof(Enums.ConfidenceLevel))]
    public string? FitnessConfidence { get; init; }

    // optional “label” for UI timeline cards
    public string? MicroWinTitle { get; init; }
}