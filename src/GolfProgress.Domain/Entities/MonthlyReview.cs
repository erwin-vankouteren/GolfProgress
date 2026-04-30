namespace GolfProgress.Domain.Entities;
using System.Text.Json;
using GolfProgress.Domain.Enums;


public class MonthlyReview
{
    public Guid MonthlyReviewId { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public DateOnly MonthStart { get; set; } // first day of month
    public decimal? FitnessAvg { get; set; }
    public decimal? FitnessChange { get; set; }

    public JsonDocument HighlightsJson { get; set; } = JsonDocument.Parse("{}");
    public string? FocusNextMonth { get; set; }

    public DateTimeOffset ComputedAt { get; set; }
}
