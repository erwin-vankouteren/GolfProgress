using GolfProgress.Domain.Enums;

namespace GolfProgress.Domain.Entities;

/// <summary>
/// One sending of a digest email (#240, #245): the daily feedback digest or the weekly coach
/// digest for one period. The key is the lock: the instance whose insert succeeds sends it,
/// so each digest goes out once even with several API instances. Each run covers what
/// happened since the previous one of its kind.
/// </summary>
public sealed class DigestRun
{
    public DigestKind Kind { get; set; }

    /// <summary>The day it's for: the date of the daily send, or of the weekly one.</summary>
    public DateOnly Period { get; set; }

    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Emails sent.</summary>
    public int Sent { get; set; }
}
