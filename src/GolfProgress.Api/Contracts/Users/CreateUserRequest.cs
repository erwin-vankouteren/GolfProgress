namespace GolfProgress.Api.Contracts.Users
{
    public sealed class CreateUserRequest
    {
        public string? Email { get; init; }
        public string? DisplayName { get; init; }
        public string? Timezone { get; init; }
        public string Units { get; init; } = "Metric";           // Metric | Imperial
        public string TrackingMode { get; init; } = "Minimal";   // Minimal | Balanced | Detailed

        public decimal? HandicapIndex { get; init; }
        public int? Typical18Score { get; init; }

        public List<string> GoalFocus { get; init; } = new();    // e.g. ["putting","penalties"]

    }
}
