namespace GolfProgress.Api.Contracts.Timeline;

public sealed class RoundTimelineResponse
{
    public Guid UserId { get; init; }
    public int Count { get; init; }
    public List<RoundTimelinePointResponse> Items { get; init; } = new();
}
