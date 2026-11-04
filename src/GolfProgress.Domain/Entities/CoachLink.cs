using GolfProgress.Domain.Enums;

namespace GolfProgress.Domain.Entities;

/// <summary>
/// A coach's access to a golfer, created when the coach accepts an invitation (#184).
/// It ends when either side ends it, and isn't reused: inviting the same coach again
/// creates a new link. At most one link per golfer and coach is active.
/// </summary>
public sealed class CoachLink
{
    public Guid CoachLinkId { get; set; }

    public Guid GolferId { get; set; }
    public User Golfer { get; set; } = default!;

    public Guid CoachId { get; set; }
    public User Coach { get; set; } = default!;

    /// <summary>Rounds and progress are always shared; these two are the golfer's choice.</summary>
    public bool ShareReflections { get; set; }
    public bool SharePractice { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public CoachLinkEndedBy? EndedBy { get; set; }
}
