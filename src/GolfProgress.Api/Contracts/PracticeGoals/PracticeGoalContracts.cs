using System.ComponentModel.DataAnnotations;
using GolfProgress.Api.Contracts.PracticeSessions;
using GolfProgress.Api.OpenApi;
using GolfProgress.Domain.Enums;

namespace GolfProgress.Api.Contracts.PracticeGoals;

/// <summary>
/// A coach's practice goal (#226, docs/coach-access.md → "Practice goals"). The golfer sees
/// every coach's; a coach only the ones they set.
/// </summary>
public sealed class PracticeGoalResponse
{
    public Guid PracticeGoalId { get; init; }
    public Guid GolferId { get; init; }

    /// <summary>The coach who set it; null once their account is deleted ("a former coach").</summary>
    public Guid? CoachId { get; init; }

    /// <summary>Their display name, never their email; null once their account is deleted.</summary>
    public string? CoachName { get; init; }

    public string Title { get; init; } = "";
    public string? Details { get; init; }

    [StringEnum(typeof(GoalFocusArea))]
    public string? FocusArea { get; init; }

    [StringEnum(typeof(SessionType), Open = true)]
    public string? SessionType { get; init; }

    public int TargetSessions { get; init; }

    /// <summary>Sessions the golfer linked to it so far: "3 of 6 sessions".</summary>
    public int LinkedSessions { get; init; }

    public string StartsOn { get; init; } = "";
    public string EndsOn { get; init; } = "";

    /// <summary>Active, Done or Dismissed; Ended for an active goal whose period is over.</summary>
    [StringEnum(typeof(PracticeGoalState))]
    public string Status { get; init; } = "";

    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
}

/// <summary>A goal and the sessions linked to it, newest first.</summary>
public sealed class PracticeGoalDetailResponse
{
    public PracticeGoalResponse Goal { get; init; } = new();
    public List<PracticeSessionResponse> Sessions { get; init; } = new();
}

/// <summary>A goal as the API shows its status: Ended is an active goal past its period.</summary>
public enum PracticeGoalState { Active, Ended, Done, Dismissed }

/// <summary>
/// Set or change a practice goal (#226). Title 1–100 characters, 1–50 sessions, a period of
/// at most 8 weeks that hasn't already ended.
/// </summary>
public sealed class PracticeGoalRequest : IValidatableObject
{
    public const int TitleMaxLength = 100;
    public const int DetailsMaxLength = 1000;
    public const int MaxDays = 56;

    [Required, MaxLength(TitleMaxLength)]
    public string Title { get; init; } = "";

    [MaxLength(DetailsMaxLength)]
    public string? Details { get; init; }

    /// <summary>The stat "Did it work?" compares: Putting, Penalties, TeeShots or Consistency.</summary>
    [StringEnum(typeof(GoalFocusArea))]
    public string? FocusArea { get; init; }

    [StringEnum(typeof(SessionType))]
    public string? SessionType { get; init; }

    [Range(1, 50)]
    public int TargetSessions { get; init; }

    /// <summary>yyyy-MM-dd; default today.</summary>
    public string? StartsOn { get; init; }

    /// <summary>yyyy-MM-dd, at most 8 weeks after the start.</summary>
    [Required]
    public string EndsOn { get; init; } = "";

    internal GoalFocusArea? ParsedFocusArea =>
        FocusArea is null ? null : Enum.Parse<GoalFocusArea>(FocusArea, ignoreCase: true);

    internal SessionType? ParsedSessionType =>
        SessionType is null ? null : Enum.Parse<SessionType>(SessionType, ignoreCase: true);

    internal DateOnly ParsedStartsOn(DateOnly today) => StartsOn is null ? today : IsoDate.Parse(StartsOn);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Title))
            yield return new ValidationResult("Title can't be empty.", new[] { nameof(Title) });

        if (FocusArea is not null && !Enum.TryParse<GoalFocusArea>(FocusArea, ignoreCase: true, out _))
            yield return new ValidationResult(
                $"FocusArea must be one of: {string.Join(", ", Enum.GetNames<GoalFocusArea>())}.", new[] { nameof(FocusArea) });

        if (SessionType is not null && !Enum.TryParse<SessionType>(SessionType, ignoreCase: true, out _))
            yield return new ValidationResult(
                $"SessionType must be one of: {string.Join(", ", Enum.GetNames<SessionType>())}.", new[] { nameof(SessionType) });

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        DateOnly? starts = today;
        if (StartsOn is not null)
        {
            if (IsoDate.TryParse(StartsOn, out var s)) starts = s;
            else
            {
                starts = null;
                yield return new ValidationResult("StartsOn must be ISO date format: yyyy-MM-dd", new[] { nameof(StartsOn) });
            }
        }

        if (!IsoDate.TryParse(EndsOn, out var ends))
            yield return new ValidationResult("EndsOn must be ISO date format: yyyy-MM-dd", new[] { nameof(EndsOn) });
        else if (starts is { } start)
        {
            if (ends < start)
                yield return new ValidationResult("EndsOn can't be before StartsOn.", new[] { nameof(EndsOn) });
            else if (ends.DayNumber - start.DayNumber > MaxDays)
                yield return new ValidationResult("A goal can last at most 8 weeks.", new[] { nameof(EndsOn) });
            else if (ends < today)
                yield return new ValidationResult("EndsOn can't be in the past.", new[] { nameof(EndsOn) });
        }
    }
}

/// <summary>The golfer marks a goal done, or dismisses it (#226).</summary>
public sealed class UpdatePracticeGoalStatusRequest
{
    /// <summary>Done or Dismissed.</summary>
    [Required, StringEnum(typeof(PracticeGoalStatusChange))]
    public string Status { get; init; } = "";
}

/// <summary>What the golfer can set a goal's status to.</summary>
public enum PracticeGoalStatusChange { Done, Dismissed }
