using GolfProgress.Domain.Enums;

namespace GolfProgress.Domain.Entities;

/// <summary>
/// A message in a feedback thread on a golfer's round, reflection or practice session
/// (#212). Each coach has their own thread with the golfer on each item, so a coach never
/// sees the golfer's other coaches; the golfer sees all of them. Either side can write first.
/// </summary>
public sealed class FeedbackMessage
{
    public Guid FeedbackMessageId { get; set; }

    public Guid GolferId { get; set; }
    public User Golfer { get; set; } = default!;

    /// <summary>The coach whose thread this is; null once their account is deleted.</summary>
    public Guid? CoachId { get; set; }
    public User? Coach { get; set; }

    /// <summary>The golfer or the coach; null once the author's account is deleted.</summary>
    public Guid? AuthorId { get; set; }
    public User? Author { get; set; }

    public FeedbackTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }

    public string Text { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? EditedAt { get; set; }
}
