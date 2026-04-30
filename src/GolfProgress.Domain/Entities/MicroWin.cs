namespace GolfProgress.Domain.Entities;
using GolfProgress.Domain.Enums;


using System.Text.Json;

public class MicroWin
{
    public Guid MicroWinId { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public Guid RoundId { get; set; }
    public Round Round { get; set; } = default!;

    public MicroWinCategory Category { get; set; }
    public string? Metric { get; set; }
    public MicroWinDirection Direction { get; set; }
    public decimal? DeltaValue { get; set; }

    public ConfidenceLevel ConfidenceLevel { get; set; } = ConfidenceLevel.Low;

    public string Title { get; set; } = default!;
    public string Message { get; set; } = default!;

    public JsonDocument EvidenceJson { get; set; } = JsonDocument.Parse("{}");

    public DateTimeOffset CreatedAt { get; set; }
}
