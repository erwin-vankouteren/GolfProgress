namespace GolfProgress.Api.Contracts.Timeline;

public sealed class MonthlyTimelineResponse
{
    public Guid UserId { get; init; }
    public int Months { get; init; }
    public List<MonthlyTimelinePointResponse> Items { get; init; } = new();
}