namespace GolfProgress.Domain.Entities; 
using System.Text.Json;
using GolfProgress.Domain.Enums;

public class FitnessScore
{
    public Guid FitnessScoreId { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public Guid RoundId { get; set; }
    public Round Round { get; set; } = default!;

    public short ScoreTotal { get; set; } // 0..100
    public short Execution { get; set; }  // 0..25
    public short DamageControl { get; set; } // 0..25
    public short Consistency { get; set; } // 0..25
    public short Habits { get; set; } // 0..25

    public ConfidenceLevel ConfidenceLevel { get; set; } = ConfidenceLevel.Low;

    // Store as JSON in SQL Server
    public JsonDocument ExplanationJson { get; set; } = JsonDocument.Parse("{}");

    public DateTimeOffset ComputedAt { get; set; }
}
