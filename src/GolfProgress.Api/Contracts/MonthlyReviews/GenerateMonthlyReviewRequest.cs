using System.ComponentModel.DataAnnotations;

namespace GolfProgress.Api.Contracts.MonthlyReviews;

public sealed class GenerateMonthlyReviewRequest
{
    public const int FocusNextMonthMaxLength = 200;

    /// <summary>The first day of the month to review, e.g. "2026-04-01".</summary>
    [Required] public string MonthStart { get; init; } = "";

    /// <summary>
    /// What to focus on next month, in the user's words: at most 200 characters once
    /// trimmed, counted as characters rather than UTF-16 units, so an emoji counts once.
    /// Left out or blank keeps the review's current focus.
    /// </summary>
    public string? FocusNextMonth { get; init; }
}
