namespace GolfProgress.Domain.Enums;

/// <summary>The digest emails (docs/coach-access.md → "Notifications (N4)").</summary>
public enum DigestKind
{
    /// <summary>Daily, to golfers with unread feedback from a coach.</summary>
    Feedback,

    /// <summary>Weekly, to coaches: each student's new rounds, sessions and replies.</summary>
    Coach,
}
