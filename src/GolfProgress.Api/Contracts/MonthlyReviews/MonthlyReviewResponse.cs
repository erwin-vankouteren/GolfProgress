using GolfProgress.Api.OpenApi;
using Enums = GolfProgress.Domain.Enums;

namespace GolfProgress.Api.Contracts.MonthlyReviews;

public sealed class MonthlyReviewResponse
{
    public Guid MonthlyReviewId { get; init; }
    public Guid UserId { get; init; }
    public string MonthStart { get; init; } = "";

    public decimal? FitnessAvg { get; init; }
    public decimal? FitnessChange { get; init; }

    public string? FocusNextMonth { get; init; }

    /// <summary>
    /// Rounds played in the month when the review was generated. Null for reviews
    /// generated before this was stored (#162).
    /// </summary>
    public int? RoundsLogged { get; init; }

    /// <summary>
    /// The month's most common micro-win category, or null when it had none or the
    /// review predates this (#162).
    /// </summary>
    [StringEnum(typeof(Enums.MicroWinCategory), Open = true)]
    public string? TopMicroWinCategory { get; init; }

    /// <summary>
    /// English sentences for display only: clients that localise use the fields above
    /// instead (#162). Kept for compatibility.
    /// </summary>
    public List<string> Highlights { get; init; } = new();

    public DateTimeOffset ComputedAt { get; init; }
}
