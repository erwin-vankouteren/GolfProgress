using GolfProgress.Domain.Enums;

namespace GolfProgress.Domain.Entities;

/// <summary>
/// How far a user has read the feedback on one of a golfer's items (#212, #220): the golfer
/// every coach's thread on it, a coach their own. Messages after ReadUpTo are unread. Per
/// item, so opening one thread leaves the others unread.
/// </summary>
public sealed class FeedbackRead
{
    public Guid UserId { get; set; }
    public Guid GolferId { get; set; }
    public FeedbackTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public DateTimeOffset ReadUpTo { get; set; }
}
