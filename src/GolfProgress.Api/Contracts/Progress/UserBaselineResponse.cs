using GolfProgress.Api.OpenApi;
using Enums = GolfProgress.Domain.Enums;
namespace GolfProgress.Api.Contracts.Progress
{
    public sealed class UserBaselineResponse
    {
        public Guid UserId { get; init; }
        /// <summary>How many recent rounds the baseline averages at most (configuration).</summary>
        public short WindowRounds { get; init; }

        /// <summary>
        /// How many rounds it actually averages: fewer than WindowRounds until that many
        /// are logged, as in "Based on 3 rounds" (#107).
        /// </summary>
        public int SampleSize { get; init; }
        public decimal? AvgScore { get; init; }
        public decimal? ScoreStdDev { get; init; }

        public decimal? AvgPutts { get; init; }
        public decimal? AvgPenalties { get; init; }
        public decimal? AvgThreePutts { get; init; }
        public decimal? AvgBlowUpHoles { get; init; }
        public decimal? AvgFairwaysHit { get; init; }
        public decimal? AvgGir { get; init; }

        public decimal CompletenessRatio { get; init; }
        [StringEnum(typeof(Enums.ConfidenceLevel))]
        public string ConfidenceLevel { get; init; } = "Low";
        public DateTimeOffset ComputedAt { get; init; }
    }

}
