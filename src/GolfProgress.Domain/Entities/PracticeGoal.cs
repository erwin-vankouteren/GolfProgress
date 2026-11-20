using GolfProgress.Domain.Enums;

namespace GolfProgress.Domain.Entities;

/// <summary>
/// A goal a coach sets for the golfer's next practice sessions (#226, docs/coach-access.md →
/// "Practice goals"). The golfer links sessions to it, marks it done or dismisses it. It stays
/// with the golfer when the coach stops or deletes their account: "from a former coach".
/// </summary>
public sealed class PracticeGoal
{
    public Guid PracticeGoalId { get; set; }

    public Guid GolferId { get; set; }
    public User Golfer { get; set; } = default!;

    /// <summary>The coach who set it; null once their account is deleted.</summary>
    public Guid? CoachId { get; set; }
    public User? Coach { get; set; }

    public string Title { get; set; } = "";
    public string? Details { get; set; }

    /// <summary>The stat "Did it work?" compares; none for a goal without one.</summary>
    public GoalFocusArea? FocusArea { get; set; }

    /// <summary>The kind of session it's for, so logging one of those suggests it.</summary>
    public SessionType? SessionType { get; set; }

    public short TargetSessions { get; set; }
    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }

    public PracticeGoalStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
