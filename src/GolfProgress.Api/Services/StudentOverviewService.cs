using GolfProgress.Api.Contracts.Coaching;
using GolfProgress.Api.Controllers;
using GolfProgress.Api.Security;
using GolfProgress.Domain.Entities;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GolfProgress.Api.Services;

/// <summary>
/// A coach's overview of a student before a lesson (#202). Built from the same services as
/// the golfer's own dashboard and progress, so both see the same numbers.
/// </summary>
public sealed class StudentOverviewService
{
    private const int RecentRounds = 10;
    private const int LatestReflections = 3;

    private readonly GolfProgressDbContext _db;
    private readonly DashboardService _dashboard;
    private readonly TimelineService _timeline;
    private readonly PracticeGoalService _goals;

    public StudentOverviewService(
        GolfProgressDbContext db, DashboardService dashboard, TimelineService timeline, PracticeGoalService goals)
    {
        _db = db;
        _dashboard = dashboard;
        _timeline = timeline;
        _goals = goals;
    }

    /// <summary>The overview for an active link, which the caller has checked with <see cref="CoachAccess"/>.</summary>
    public async Task<StudentOverviewResponse> BuildAsync(CoachLink link, CancellationToken ct)
    {
        var golferId = link.GolferId;
        var dashboard = await _dashboard.BuildAsync(golferId, ct);
        var rounds = await _timeline.GetRoundTimelineAsync(golferId, RecentRounds, from: null, to: null, ct);

        var baseline = await _db.UserBaselines.AsNoTracking().FirstOrDefaultAsync(b => b.UserId == golferId, ct);
        var roundCount = baseline is null ? 0 : await _db.Rounds.CountAsync(r => r.UserId == golferId, ct);

        var reflections = new List<StudentReflectionResponse>();
        if (CoachAccess.Covers(link, CoachShare.Reflections))
        {
            reflections = (await (
                    from r in _db.Reflections.AsNoTracking().Where(r => r.UserId == golferId)
                    join round in _db.Rounds.AsNoTracking() on r.RoundId equals round.RoundId into rr
                    from round in rr.DefaultIfEmpty()
                    orderby r.CreatedAt descending
                    select new { Reflection = r, PlayedAt = (DateOnly?)round.PlayedAt })
                .Take(LatestReflections)
                .ToListAsync(ct))
                .Select(x => new StudentReflectionResponse
                {
                    ReflectionId = x.Reflection.ReflectionId,
                    RoundId = x.Reflection.RoundId,
                    PlayedAt = x.PlayedAt?.ToString("yyyy-MM-dd"),
                    FocusRating = x.Reflection.FocusRating,
                    MoodRating = x.Reflection.MoodRating,
                    FrustrationSpillover = x.Reflection.FrustrationSpillover,
                    WhatWorked = x.Reflection.WhatWorked,
                    WhatToRepeat = x.Reflection.WhatToRepeat,
                    OneChangeNext = x.Reflection.OneChangeNext,
                    CreatedAt = x.Reflection.CreatedAt,
                })
                .ToList();
        }

        return new StudentOverviewResponse
        {
            GolferId = golferId,
            DisplayName = CoachInvitationService.DisplayNameOf(link.Golfer),
            ShareReflections = link.ShareReflections,
            SharePractice = link.SharePractice,
            LastRound = dashboard.LastRound,
            RecentRounds = rounds.Items,
            MonthlyFitnessAvg = dashboard.MonthlyFitnessAvg,
            MonthlyFitnessChange = dashboard.MonthlyFitnessChange,
            NextFocus = dashboard.NextLever,
            Baseline = baseline is null ? null : ProgressController.BaselineResponse(baseline, roundCount),
            LatestReflections = reflections,
            ActiveGoals = CoachAccess.Covers(link, CoachShare.Practice)
                ? await _goals.CurrentForCoachAsync(link.CoachId, golferId, ct)
                : new(),
        };
    }
}
