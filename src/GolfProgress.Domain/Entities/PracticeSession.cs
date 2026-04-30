namespace GolfProgress.Domain.Entities;
using GolfProgress.Domain.Enums;

public class PracticeSession
{
    public Guid PracticeSessionId { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public DateOnly PracticedAt { get; set; }
    public short? DurationMinutes { get; set; }
    public SessionType SessionType { get; set; } = SessionType.Range;
    public string? VenueName { get; set; }

    // Stored as JSON in SQL Server (nvarchar(max))
    public List<string> Tags { get; set; } = new();

    public string? PrimaryGoal { get; set; }
    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
