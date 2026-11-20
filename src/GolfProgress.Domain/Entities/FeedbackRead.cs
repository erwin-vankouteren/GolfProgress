namespace GolfProgress.Domain.Entities;

/// <summary>
/// How far a user has read a golfer's feedback threads (#212): the golfer their own, a
/// coach the threads with that golfer. Messages after ReadUpTo are unread.
/// </summary>
public sealed class FeedbackRead
{
    public Guid UserId { get; set; }
    public Guid GolferId { get; set; }
    public DateTimeOffset ReadUpTo { get; set; }
}
