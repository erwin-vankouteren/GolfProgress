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

/// <summary>Mark a golfer's threads read up to a time: the golfer their own, a coach theirs with the golfer.</summary>
public sealed class MarkFeedbackReadRequest
{
    [Required] public Guid? GolferId { get; init; }

    /// <summary>Default now; a later time counts as now, and an earlier one than before changes nothing.</summary>
    public DateTimeOffset? UpTo { get; init; }
}

public static class FeedbackLimits
{
    public const int MaxTextLength = 2000;

    /// <summary>Messages a user can write an hour, so a slip or a script can't flood a thread.</summary>
    public const int MaxPerHour = 60;
}
