using System.Text.Json;
using GolfProgress.Api.Contracts.PracticeGoals;
using GolfProgress.Api.Controllers;
using GolfProgress.Domain.Entities;
using GolfProgress.Domain.Enums;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GolfProgress.Api.Services;

/// <summary>Why a goal couldn't be removed.</summary>
public enum PracticeGoalProblem
{
    NotFound,
    /// <summary>Sessions are linked to it: it stays as their record, and can be ended instead.</summary>
    HasSessions,
}

/// <summary>
/// Practice goals (#226, #229, docs/coach-access.md → "Practice goals"). A coach sets them
/// for a golfer who shares practice, and sees and changes only their own; the golfer sees
/// every coach's, links sessions to the active ones, and marks them done or dismisses them.
/// Callers check the coach's access (<see cref="Security.CoachAccess"/>) first.
/// </summary>
public sealed class PracticeGoalService
{
    private readonly GolfProgressDbContext _db;

    public PracticeGoalService(GolfProgressDbContext db) => _db = db;

    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>Active, and within its period: sessions can be linked to it.</summary>
    public static bool IsCurrent(PracticeGoal goal, DateOnly today) =>
        goal.Status == PracticeGoalStatus.Active && goal.StartsOn <= today && today <= goal.EndsOn;

    /// <summary>The golfer's goals from every coach: current ones first, then the rest, newest first.</summary>
    public Task<List<PracticeGoalResponse>> ForGolferAsync(Guid golferId, CancellationToken ct) =>
        ListAsync(_db.PracticeGoals.Where(g => g.GolferId == golferId), ct);

    /// <summary>The goals [coachId] set for the golfer.</summary>
    public Task<List<PracticeGoalResponse>> ForCoachAsync(Guid coachId, Guid golferId, CancellationToken ct) =>
        ListAsync(_db.PracticeGoals.Where(g => g.GolferId == golferId && g.CoachId == coachId), ct);

    /// <summary>A goal with its linked sessions; null when it isn't the golfer's, or for a coach, theirs.</summary>
    public async Task<PracticeGoalDetailResponse?> DetailAsync(
        Guid golferId, Guid goalId, Guid? coachId, bool includeNotes, CancellationToken ct)
    {
        var goal = await FindAsync(golferId, goalId, coachId, ct);
        if (goal is null) return null;

        var sessions = await _db.PracticeSessions.AsNoTracking()
            .Where(s => s.UserId == golferId && s.PracticeGoalId == goalId)
            .OrderByDescending(s => s.PracticedAt).ThenByDescending(s => s.CreatedAt)
            .ToListAsync(ct);
        return new PracticeGoalDetailResponse
        {
            Goal = (await ResponsesAsync(new List<PracticeGoal> { goal }, ct)).Single(),
            Sessions = sessions.Select(s => PracticeSessionsController.ToResponse(s, includeNotes)).ToList(),
        };
    }

    /// <summary>A coach sets a goal for the golfer, in the golfer's log.</summary>
    public async Task<PracticeGoalResponse> CreateAsync(Guid coachId, Guid golferId, PracticeGoalRequest request, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var goal = new PracticeGoal
        {
            PracticeGoalId = Guid.NewGuid(),
            GolferId = golferId,
            CoachId = coachId,
            Status = PracticeGoalStatus.Active,
            CreatedAt = now,
        };
        Apply(goal, request);
        _db.PracticeGoals.Add(goal);
        _db.Events.Add(new EventLog
        {
            EventLogId = Guid.NewGuid(),
            UserId = golferId,
            EventType = "PracticeGoalSet",
            RelatedId = goal.PracticeGoalId,
            PayloadJson = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                practiceGoalId = goal.PracticeGoalId,
                focusArea = goal.FocusArea?.ToString(),
                targetSessions = goal.TargetSessions,
                endsOn = goal.EndsOn.ToString("yyyy-MM-dd"),
            })),
            CreatedAt = now,
        });
        await _db.SaveChangesAsync(ct);
        return (await ResponsesAsync(new List<PracticeGoal> { goal }, ct)).Single();
    }

    /// <summary>The coach changes their goal; null when it isn't theirs.</summary>
    public async Task<PracticeGoalResponse?> UpdateAsync(
        Guid coachId, Guid golferId, Guid goalId, PracticeGoalRequest request, CancellationToken ct)
    {
        var goal = await _db.PracticeGoals.FirstOrDefaultAsync(
            g => g.PracticeGoalId == goalId && g.GolferId == golferId && g.CoachId == coachId, ct);
        if (goal is null) return null;

        Apply(goal, request);
        goal.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return (await ResponsesAsync(new List<PracticeGoal> { goal }, ct)).Single();
    }

    /// <summary>The coach removes their goal, unless sessions are linked to it.</summary>
    public async Task<PracticeGoalProblem?> DeleteAsync(Guid coachId, Guid golferId, Guid goalId, CancellationToken ct)
    {
        var goal = await _db.PracticeGoals.FirstOrDefaultAsync(
            g => g.PracticeGoalId == goalId && g.GolferId == golferId && g.CoachId == coachId, ct);
        if (goal is null) return PracticeGoalProblem.NotFound;
        if (await _db.PracticeSessions.AnyAsync(s => s.PracticeGoalId == goalId, ct)) return PracticeGoalProblem.HasSessions;

        _db.PracticeGoals.Remove(goal);
        await _db.SaveChangesAsync(ct);
        return null;
    }

    /// <summary>The golfer marks one of their goals done or dismisses it; null when there's no such goal.</summary>
    public async Task<PracticeGoalResponse?> SetStatusAsync(
        Guid golferId, Guid goalId, PracticeGoalStatus status, CancellationToken ct)
    {
        var goal = await _db.PracticeGoals.FirstOrDefaultAsync(g => g.PracticeGoalId == goalId && g.GolferId == golferId, ct);
        if (goal is null) return null;

        if (goal.Status != status)
        {
            goal.Status = status;
            goal.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        return (await ResponsesAsync(new List<PracticeGoal> { goal }, ct)).Single();
    }

    /// <summary>
    /// Whether the golfer can link a session on [practicedAt] to the goal: theirs, active,
    /// and the session falls within its period.
    /// </summary>
    public async Task<bool> CanLinkAsync(Guid golferId, Guid goalId, DateOnly practicedAt, CancellationToken ct) =>
        await _db.PracticeGoals.AnyAsync(
            g => g.PracticeGoalId == goalId && g.GolferId == golferId && g.Status == PracticeGoalStatus.Active
                 && g.StartsOn <= practicedAt && practicedAt <= g.EndsOn,
            ct);

    /// <summary>The coach's current goals for a student, for the overview (#202).</summary>
    public async Task<List<PracticeGoalResponse>> CurrentForCoachAsync(Guid coachId, Guid golferId, CancellationToken ct)
    {
        var today = Today;
        return await ListAsync(_db.PracticeGoals.Where(g =>
            g.GolferId == golferId && g.CoachId == coachId && g.Status == PracticeGoalStatus.Active
            && g.StartsOn <= today && today <= g.EndsOn), ct);
    }

    /// <summary>"Did it work?" for a goal (#234); null when it isn't the golfer's, or for a coach, theirs.</summary>
    public async Task<GoalOutcomeResponse?> OutcomeAsync(Guid golferId, Guid goalId, Guid? coachId, CancellationToken ct)
    {
        var goal = await FindAsync(golferId, goalId, coachId, ct);
        if (goal is null) return null;

        var last = goal.EndsOn.AddDays(GoalOutcome.DaysAfter);
        var rounds = await _db.Rounds.AsNoTracking()
            .Where(r => r.UserId == golferId && r.PlayedAt <= last)
            .ToListAsync(ct);
        var result = GoalOutcome.Compute(goal.FocusArea, goal.StartsOn, goal.EndsOn, rounds);
        return new GoalOutcomeResponse
        {
            FocusArea = goal.FocusArea?.ToString(),
            Stat = result.Stat?.ToString(),
            HigherIsBetter = result.HigherIsBetter,
            Before = result.Before,
            During = result.During,
            Change = result.Change,
            RoundsBefore = result.RoundsBefore,
            RoundsDuring = result.RoundsDuring,
            DuringFrom = goal.StartsOn.ToString("yyyy-MM-dd"),
            DuringTo = last.ToString("yyyy-MM-dd"),
            Confidence = result.Confidence.ToString(),
            Verdict = result.Verdict.ToString(),
        };
    }

    private async Task<PracticeGoal?> FindAsync(Guid golferId, Guid goalId, Guid? coachId, CancellationToken ct) =>
        await _db.PracticeGoals.AsNoTracking().FirstOrDefaultAsync(
            g => g.PracticeGoalId == goalId && g.GolferId == golferId && (coachId == null || g.CoachId == coachId), ct);

    private async Task<List<PracticeGoalResponse>> ListAsync(IQueryable<PracticeGoal> query, CancellationToken ct)
    {
        var goals = await query.AsNoTracking().ToListAsync(ct);
        var today = Today;
        var ordered = goals
            .OrderByDescending(g => IsCurrent(g, today))
            .ThenByDescending(g => g.CreatedAt)
            .ToList();
        return await ResponsesAsync(ordered, ct);
    }

    private async Task<List<PracticeGoalResponse>> ResponsesAsync(List<PracticeGoal> goals, CancellationToken ct)
    {
        var ids = goals.Select(g => g.PracticeGoalId).ToList();
        var linked = await _db.PracticeSessions.AsNoTracking()
            .Where(s => s.PracticeGoalId != null && ids.Contains(s.PracticeGoalId.Value))
            .GroupBy(s => s.PracticeGoalId!.Value)
            .Select(g => new { GoalId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.GoalId, x => x.Count, ct);
        var coachIds = goals.Select(g => g.CoachId).OfType<Guid>().Distinct().ToList();
        var coaches = await _db.Users.AsNoTracking().Where(u => coachIds.Contains(u.UserId)).ToListAsync(ct);
        var names = coaches.ToDictionary(u => u.UserId, CoachInvitationService.DisplayNameOf);
        var today = Today;

        return goals.Select(g => new PracticeGoalResponse
        {
            PracticeGoalId = g.PracticeGoalId,
            GolferId = g.GolferId,
            CoachId = g.CoachId,
            CoachName = g.CoachId is { } c && names.TryGetValue(c, out var name) ? name : null,
            Title = g.Title,
            Details = g.Details,
            FocusArea = g.FocusArea?.ToString(),
            SessionType = g.SessionType?.ToString(),
            TargetSessions = g.TargetSessions,
            LinkedSessions = linked.GetValueOrDefault(g.PracticeGoalId),
            StartsOn = g.StartsOn.ToString("yyyy-MM-dd"),
            EndsOn = g.EndsOn.ToString("yyyy-MM-dd"),
            Status = StateOf(g, today).ToString(),
            CreatedAt = g.CreatedAt,
            UpdatedAt = g.UpdatedAt,
        }).ToList();
    }

    private static PracticeGoalState StateOf(PracticeGoal goal, DateOnly today) => goal.Status switch
    {
        PracticeGoalStatus.Done => PracticeGoalState.Done,
        PracticeGoalStatus.Dismissed => PracticeGoalState.Dismissed,
        _ => today > goal.EndsOn ? PracticeGoalState.Ended : PracticeGoalState.Active,
    };

    private static void Apply(PracticeGoal goal, PracticeGoalRequest request)
    {
        goal.Title = request.Title.Trim();
        goal.Details = string.IsNullOrWhiteSpace(request.Details) ? null : request.Details.Trim();
        goal.FocusArea = request.ParsedFocusArea;
        goal.SessionType = request.ParsedSessionType;
        goal.TargetSessions = (short)request.TargetSessions;
        goal.StartsOn = request.ParsedStartsOn(Today);
        goal.EndsOn = Contracts.IsoDate.Parse(request.EndsOn);
    }
}
