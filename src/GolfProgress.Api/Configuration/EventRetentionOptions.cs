using System.ComponentModel.DataAnnotations;

namespace GolfProgress.Api.Configuration;

/// <summary>
/// How long audit events are kept (#20). Without this the Events table, which is
/// appended to on nearly every write, would grow without bound.
/// </summary>
public sealed class EventRetentionOptions
{
    public const string SectionName = "EventRetention";

    /// <summary>Turns the background purge off, e.g. in tests.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Events older than this are deleted.</summary>
    [Range(1, 3650)]
    public int RetentionDays { get; set; } = 365;

    /// <summary>How often the purge runs.</summary>
    [Range(1, 168)]
    public int PurgeIntervalHours { get; set; } = 24;
}
