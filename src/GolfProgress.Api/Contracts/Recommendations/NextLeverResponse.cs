namespace GolfProgress.Api.Contracts.Recommendations;

public sealed class NextLeverResponse
{
    public Guid RoundId { get; init; }
    public Guid UserId { get; init; }

    public string LeverKey { get; init; } = "";      // e.g. "penalties", "three_putts"
    public string Title { get; init; } = "";         // short
    public string Recommendation { get; init; } = "";// one actionable sentence
    public string Rationale { get; init; } = "";     // why this was chosen

    public string ConfidenceLevel { get; init; } = "Low";
    public DateTimeOffset CreatedAt { get; init; }
}