namespace GolfProgress.Api.Contracts.MonthlyReviews;

public sealed class MonthlyReviewResponse
{
    public Guid MonthlyReviewId { get; init; }
    public Guid UserId { get; init; }
    public string MonthStart { get; init; } = "";

    public decimal? FitnessAvg { get; init; }
    public decimal? FitnessChange { get; init; }

    public string? FocusNextMonth { get; init; }

    // Keep it simple: a few highlight strings in v1
    public List<string> Highlights { get; init; } = new();

    public DateTimeOffset ComputedAt { get; init; }
}
