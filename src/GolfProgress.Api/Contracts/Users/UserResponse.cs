using GolfProgress.Api.OpenApi;
using Enums = GolfProgress.Domain.Enums;
using Services = GolfProgress.Api.Services;
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

        /// <summary>Whether someone's invitation made this user their coach (#199).</summary>
        public bool HasStudents { get; init; }

        /// <summary>The daily email about unread feedback from a coach (#240).</summary>
        public bool EmailFeedbackDigest { get; init; } = true;

        /// <summary>The weekly email about students' activity, for coaches (#245).</summary>
        public bool EmailCoachDigest { get; init; } = true;

        /// <summary>What the user can do (#309, docs/payments.md): the trial, a subscription, or read-only.</summary>
        [StringEnum(typeof(Services.Plan))]
        public string Plan { get; init; } = "Trial";

        /// <summary>When the free trial ends, or ended; null in the launch period, before subscriptions start.</summary>
        public DateTimeOffset? TrialEndsAt { get; init; }

        /// <summary>Whether coaching works: in the trial, or with the Coaching add-on.</summary>
        public bool Coaching { get; init; }

        /// <summary>The subscription that gives access; null without one.</summary>
        public SubscriptionResponse? Subscription { get; init; }
    }
}
