using System.ComponentModel.DataAnnotations;

namespace GolfProgress.Api.Configuration;

public sealed class ProgressOptions
{
    public const string SectionName = "Progress";

    /// <summary>
    /// How many recent rounds form a baseline: both the window a round is scored
    /// against and the user's stored current baseline. The single source for this
    /// value (#22).
    /// </summary>
    [Range(1, 50)]
    public short BaselineWindowRounds { get; set; } = 5;
}
