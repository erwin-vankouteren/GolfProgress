using GolfProgress.Api.OpenApi;
using Enums = GolfProgress.Domain.Enums;
namespace GolfProgress.Api.Contracts.MicroWins;

public sealed class MicroWinResponse
{
    public Guid MicroWinId { get; init; }
    public Guid RoundId { get; init; }
    public Guid UserId { get; init; }

    [StringEnum(typeof(Enums.MicroWinCategory), Open = true)]
    public string Category { get; init; } = "";
    public string? Metric { get; init; }
    [StringEnum(typeof(Enums.MicroWinDirection))]
    public string Direction { get; init; } = "";
    public decimal? DeltaValue { get; init; }

    [StringEnum(typeof(Enums.ConfidenceLevel))]
    public string ConfidenceLevel { get; init; } = "Low";
    public string Title { get; init; } = "";
    public string Message { get; init; } = "";

    public DateTimeOffset CreatedAt { get; init; }
}