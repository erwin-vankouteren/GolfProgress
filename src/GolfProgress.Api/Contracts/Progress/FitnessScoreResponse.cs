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

        public string ConfidenceLevel { get; init; } = "Low";
        public DateTimeOffset ComputedAt { get; init; }
    }

}
