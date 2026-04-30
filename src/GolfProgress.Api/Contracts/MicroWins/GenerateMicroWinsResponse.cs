namespace GolfProgress.Api.Contracts.MicroWins;

public sealed class GenerateMicroWinsResponse
{
    public Guid RoundId { get; init; }
    public int GeneratedCount { get; init; }
    public List<MicroWinResponse> Items { get; init; } = new();
}