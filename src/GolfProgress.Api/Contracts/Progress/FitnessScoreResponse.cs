using GolfProgress.Api.OpenApi;
using Enums = GolfProgress.Domain.Enums;
namespace GolfProgress.Api.Contracts.Progress
{
    public sealed class FitnessScoreResponse
    {
        public Guid FitnessScoreId { get; init; }
        public Guid UserId { get; init; }
        public Guid RoundId { get; init; }

        public short ScoreTotal { get; init; }
        public short Execution { get; init; }
        public short DamageControl { get; init; }
        public short Consistency { get; init; }
        public short Habits { get; init; }

        [StringEnum(typeof(Enums.ConfidenceLevel))]
        public string ConfidenceLevel { get; init; } = "Low";
        public DateTimeOffset ComputedAt { get; init; }
    }

}
