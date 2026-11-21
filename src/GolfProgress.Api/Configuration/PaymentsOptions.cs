using System.ComponentModel.DataAnnotations;

namespace GolfProgress.Api.Configuration;

/// <summary>Subscriptions (#309, docs/payments.md).</summary>
public sealed class PaymentsOptions
{
    public const string SectionName = "Payments";

    /// <summary>
    /// When subscriptions start. Until it's set, everyone uses the app free, as in the
    /// launch period the terms describe. From then on, a trial ends 30 days after signing
    /// up, but never earlier than <see cref="TrialDays"/> after this date, so accounts that
    /// existed before get the terms' 30 days' notice.
    /// </summary>
    public DateTimeOffset? LaunchDate { get; set; }

    /// <summary>How long the free trial lasts, and how much notice existing accounts get.</summary>
    [Range(1, 365)]
    public int TrialDays { get; set; } = 30;
}
