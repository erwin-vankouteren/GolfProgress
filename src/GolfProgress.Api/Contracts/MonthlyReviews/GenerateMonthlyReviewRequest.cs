using System.ComponentModel.DataAnnotations;

namespace GolfProgress.Api.Contracts.MonthlyReviews;

public sealed class GenerateMonthlyReviewRequest
{
    // Use first day of month: "2026-04-01"
    [Required] public string MonthStart { get; init; } = "";

    // Optional: allow user to set/override focus for next month
    public string? FocusNextMonth { get; init; }
}
