using GolfProgress.Api.OpenApi;
using Enums = GolfProgress.Domain.Enums;
namespace GolfProgress.Api.Contracts.Users
{
    public sealed class UserResponse
    {
        public Guid UserId { get; init; }
        public string? Email { get; init; }
        public string? DisplayName { get; init; }
        public string? Timezone { get; init; }
        [StringEnum(typeof(Enums.Units))]
        public string Units { get; init; } = "Metric";
        [StringEnum(typeof(Enums.TrackingMode))]
        public string TrackingMode { get; init; } = "Minimal";

        /// <summary>The language chosen in the profile; null means the device's language.</summary>
        [StringEnum(typeof(Enums.AppLanguage), Open = true)]
        public string? Language { get; init; }

        public decimal? HandicapIndex { get; init; }
        public int? Typical18Score { get; init; }
        public List<string> GoalFocus { get; init; } = new();

        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset UpdatedAt { get; init; }

    }
}
