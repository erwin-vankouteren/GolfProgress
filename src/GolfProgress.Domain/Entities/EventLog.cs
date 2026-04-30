namespace GolfProgress.Domain.Entities;
using System.Text.Json;
using GolfProgress.Domain.Enums;

public class EventLog
{
    public Guid EventLogId { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public string EventType { get; set; } = default!;
    public Guid? RelatedId { get; set; }

    public JsonDocument PayloadJson { get; set; } = JsonDocument.Parse("{}");

    public DateTimeOffset CreatedAt { get; set; }
}
