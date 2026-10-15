namespace GolfProgress.Domain.Entities;
using System.Text.Json;
using GolfProgress.Domain.Enums;

/// <summary>
/// An append-only audit record of something that happened to a user's data:
/// RoundLogged, ReflectionAdded, BaselineComputed, FitnessScoreComputed,
/// MicroWinsGenerated, NextLeverRecommended and MonthlyReviewGenerated.
///
/// Nothing in the API reads these back (#20). They exist as an audit trail -- to
/// reconstruct what was computed for a user and when, e.g. when investigating a
/// score -- and as the raw material for future analytics. They are deleted after
/// EventRetention:RetentionDays (365 by default), so the table does not grow
/// without bound.
///
/// Rows are written alongside the change they describe and are never updated.
/// </summary>
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
