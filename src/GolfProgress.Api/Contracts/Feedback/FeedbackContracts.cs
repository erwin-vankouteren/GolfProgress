using System.ComponentModel.DataAnnotations;
using GolfProgress.Api.OpenApi;
using GolfProgress.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace GolfProgress.Api.Contracts.Feedback;

/// <summary>
/// A message in a feedback thread (#212). Each coach has their own thread with the golfer
/// on an item: a coach gets only theirs, the golfer gets every coach's, told apart by coachId.
/// </summary>
public sealed class FeedbackMessageResponse
{
    public Guid FeedbackMessageId { get; init; }
    public Guid GolferId { get; init; }

    /// <summary>The coach whose thread this is; null once their account is deleted ("a former coach").</summary>
    public Guid? CoachId { get; init; }

    /// <summary>That coach's display name, never their email; null once their account is deleted.</summary>
    public string? CoachName { get; init; }

    /// <summary>True when the coach wrote it, false when the golfer did.</summary>
    public bool FromCoach { get; init; }

    /// <summary>True when the caller wrote it, so they can edit or delete it.</summary>
    public bool Mine { get; init; }

    [StringEnum(typeof(FeedbackTargetType))]
    public string TargetType { get; init; } = "";
    public Guid TargetId { get; init; }

    public string Text { get; init; } = "";
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? EditedAt { get; init; }
}

/// <summary>Which thread: a golfer's round, reflection or practice session.</summary>
public sealed class FeedbackThreadQuery
{
    [Required, FromQuery(Name = "golferId")] public Guid? GolferId { get; init; }

    [Required, FromQuery(Name = "targetType"), StringEnum(typeof(FeedbackTargetType))]
    public string TargetType { get; init; } = "";

    [Required, FromQuery(Name = "targetId")] public Guid? TargetId { get; init; }
}

/// <summary>Write a message (#212). Either side can write first.</summary>
public sealed class CreateFeedbackRequest
{
    [Required] public Guid? GolferId { get; init; }

    [Required, StringEnum(typeof(FeedbackTargetType))]
    public string TargetType { get; init; } = "";

    [Required] public Guid? TargetId { get; init; }

    /// <summary>
    /// For the golfer: the coach whose thread it goes in, who must be able to see the item.
    /// Ignored for a coach, who writes in their own.
    /// </summary>
    public Guid? CoachId { get; init; }

    /// <summary>Plain text, 1–2,000 characters after trimming.</summary>
    [Required, MaxLength(FeedbackLimits.MaxTextLength)]
    public string Text { get; init; } = "";
}

/// <summary>Change your own message.</summary>
public sealed class UpdateFeedbackRequest
{
    [Required, MaxLength(FeedbackLimits.MaxTextLength)]
    public string Text { get; init; } = "";
}

/// <summary>
/// Mark the feedback on an item read up to a time: for the golfer every coach's thread on it,
/// for a coach their own. Other items stay unread.
/// </summary>
public sealed class MarkFeedbackReadRequest
{
    [Required] public Guid? GolferId { get; init; }

    [Required, StringEnum(typeof(FeedbackTargetType))]
    public string TargetType { get; init; } = "";

    [Required] public Guid? TargetId { get; init; }

    /// <summary>Default now; a later time counts as now, and an earlier one than before changes nothing.</summary>
    public DateTimeOffset? UpTo { get; init; }
}

/// <summary>"New from your coach" (#220): what the golfer hasn't read yet.</summary>
public sealed class UnreadFeedbackResponse
{
    /// <summary>Unread messages from coaches, on every item.</summary>
    public int Count { get; init; }

    /// <summary>The threads with unread messages, newest first, at most 5.</summary>
    public List<UnreadFeedbackThreadResponse> Threads { get; init; } = new();
}

/// <summary>A coach's thread with unread messages, with what it's about, to link to it.</summary>
public sealed class UnreadFeedbackThreadResponse
{
    [StringEnum(typeof(FeedbackTargetType))]
    public string TargetType { get; init; } = "";
    public Guid TargetId { get; init; }

    /// <summary>The round it's on: the round itself, or a reflection's round; null for a practice session.</summary>
    public Guid? RoundId { get; init; }

    /// <summary>The date of the round or session (yyyy-MM-dd); null for a reflection without a round.</summary>
    public string? OnDate { get; init; }

    public Guid? CoachId { get; init; }
    /// <summary>Null once the coach's account is deleted ("a former coach").</summary>
    public string? CoachName { get; init; }

    public int Unread { get; init; }
    public DateTimeOffset NewestAt { get; init; }

    /// <summary>The start of the newest unread message, at most 140 characters.</summary>
    public string Preview { get; init; } = "";
}

public static class FeedbackLimits
{
    public const int MaxTextLength = 2000;

    /// <summary>Messages a user can write an hour, so a slip or a script can't flood a thread.</summary>
    public const int MaxPerHour = 60;
}
