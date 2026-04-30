namespace GolfProgress.Api.Contracts.Progress
{
    public sealed class UserBaselineResponse
    {
        public Guid UserId { get; init; }
        public short WindowRounds { get; init; }
        public decimal? AvgScore { get; init; }
        public decimal? ScoreStdDev { get; init; }

        public decimal? AvgPutts { get; init; }
        public decimal? AvgPenalties { get; init; }
        public decimal? AvgThreePutts { get; init; }
        public decimal? AvgBlowUpHoles { get; init; }
        public decimal? AvgFairwaysHit { get; init; }
        public decimal? AvgGir { get; init; }

        public decimal CompletenessRatio { get; init; }
        public string ConfidenceLevel { get; init; } = "Low";
        public DateTimeOffset ComputedAt { get; init; }
    }

}
