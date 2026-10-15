namespace GolfProgress.Domain.Entities;
using GolfProgress.Domain.Enums;
public class UserBaseline
{
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    /// <summary>Set from configuration (Progress:BaselineWindowRounds) when computed.</summary>
    public short WindowRounds { get; set; }

    public decimal? AvgScore { get; set; }
    public decimal? ScoreStdDev { get; set; }

    public decimal? AvgPutts { get; set; }
    public decimal? AvgPenalties { get; set; }
    public decimal? AvgThreePutts { get; set; }
    public decimal? AvgBlowUpHoles { get; set; }
    public decimal? AvgFairwaysHit { get; set; }
    public decimal? AvgGir { get; set; }

    public decimal CompletenessRatio { get; set; }
    public ConfidenceLevel ConfidenceLevel { get; set; } = ConfidenceLevel.Low;

    public DateTimeOffset ComputedAt { get; set; }
}
