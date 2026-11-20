using System.ComponentModel.DataAnnotations;

namespace GolfProgress.Api.Configuration;

/// <summary>
/// The digest emails (#240, #245; docs/coach-access.md → "Notifications (N4)"): the daily
/// feedback digest to golfers and the weekly activity digest to coaches.
/// </summary>
public sealed class DigestOptions
{
    public const string SectionName = "Digests";

    /// <summary>Turns the background sending off, e.g. in tests.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>The hour (UTC) both digests go out: 7 is 8 or 9 in the morning in the Netherlands.</summary>
    [Range(0, 23)]
    public int SendHourUtc { get; set; } = 7;

    /// <summary>The day the coach digest goes out.</summary>
    public DayOfWeek CoachDigestDay { get; set; } = DayOfWeek.Monday;

    /// <summary>How often the worker checks whether a digest is due.</summary>
    [Range(1, 1440)]
    public int CheckIntervalMinutes { get; set; } = 15;
}
