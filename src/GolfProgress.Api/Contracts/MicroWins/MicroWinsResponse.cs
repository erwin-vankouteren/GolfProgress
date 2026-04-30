namespace GolfProgress.Api.Contracts.MicroWins;

public sealed class MicroWinResponse
{
    public Guid MicroWinId { get; init; }
    public Guid RoundId { get; init; }
    public Guid UserId { get; init; }

    public string Category { get; init; } = "";
    public string? Metric { get; init; }
    public string Direction { get; init; } = "";
    public decimal? DeltaValue { get; init; }

    public string ConfidenceLevel { get; init; } = "Low";
    public string Title { get; init; } = "";
    public string Message { get; init; } = "";

    public DateTimeOffset CreatedAt { get; init; }
}