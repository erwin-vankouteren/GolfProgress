namespace GolfProgress.Api.Contracts.Users
{
    public sealed class UserResponse
    {
        public Guid UserId { get; init; }
        public string? Email { get; init; }
        public string? DisplayName { get; init; }
        public string? Timezone { get; init; }
        public string Units { get; init; } = "Metric";
        public string TrackingMode { get; init; } = "Minimal";

        public decimal? HandicapIndex { get; init; }
        public int? Typical18Score { get; init; }
        public List<string> GoalFocus { get; init; } = new();

        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset UpdatedAt { get; init; }

    }
}
