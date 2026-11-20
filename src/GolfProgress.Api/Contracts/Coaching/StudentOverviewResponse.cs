using GolfProgress.Api.Contracts.Dashboard;
using GolfProgress.Api.Contracts.Progress;
using GolfProgress.Api.Contracts.Recommendations;
using GolfProgress.Api.Contracts.Timeline;

namespace GolfProgress.Api.Contracts.Coaching;

/// <summary>
/// What a coach needs before a lesson (#202, docs/coach-access.md → "What a coach sees").
/// Never the golfer's email address, preferences or handicap.
/// </summary>
public sealed class StudentOverviewResponse
{
    public Guid GolferId { get; init; }
    public string DisplayName { get; init; } = "";
    public bool ShareReflections { get; init; }
    public bool SharePractice { get; init; }

    /// <summary>The latest round with its fitness score; null before the first.</summary>
    public DashboardRoundSummary? LastRound { get; init; }

    /// <summary>The latest rounds, at most 10, oldest first, for the fitness score trend.</summary>
    public List<RoundTimelinePointResponse> RecentRounds { get; init; } = new();

    /// <summary>The average fitness score of this calendar month's rounds (UTC), or null.</summary>
    public decimal? MonthlyFitnessAvg { get; init; }

    /// <summary>This month's average minus last month's; null unless both have one.</summary>
    public decimal? MonthlyFitnessChange { get; init; }

    /// <summary>The golfer's next focus after their latest round; null before the first.</summary>
    public NextLeverResponse? NextFocus { get; init; }

    /// <summary>Null until a round is logged.</summary>
    public UserBaselineResponse? Baseline { get; init; }

    /// <summary>The newest reflections, at most 3; always empty when reflections aren't shared.</summary>
    public List<StudentReflectionResponse> LatestReflections { get; init; } = new();

    /// <summary>The caller's current practice goals for the student (#227); empty when practice isn't shared.</summary>
    public List<PracticeGoals.PracticeGoalResponse> ActiveGoals { get; init; } = new();
}

/// <summary>A round reflection as a coach sees it: with the date of its round.</summary>
public sealed class StudentReflectionResponse
{
    public Guid ReflectionId { get; init; }
    public Guid? RoundId { get; init; }

    /// <summary>The round's date (yyyy-MM-dd), or null for a reflection without a round.</summary>
    public string? PlayedAt { get; init; }

    public short? FocusRating { get; init; }
    public short? MoodRating { get; init; }
    public bool? FrustrationSpillover { get; init; }
    public string? WhatWorked { get; init; }
    public string? WhatToRepeat { get; init; }
    public string? OneChangeNext { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
