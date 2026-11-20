using GolfProgress.Api.Services;
using GolfProgress.Domain.Entities;
using GolfProgress.Domain.Enums;
using System.Net;
using System.Net.Http.Json;

namespace GolfProgress.Api.Tests;

/// <summary>Issue #234: "Did it work?", the pure calculation.</summary>
public sealed class GoalOutcomeCalculationTests
{
    private static readonly DateOnly Start = new(2026, 6, 1);
    private static readonly DateOnly End = new(2026, 6, 28);

    private static Round R(int daysFromStart, short? putts = 32, short strokes = 90, short holes = 18, short? fairways = null, short? penalties = null)
    {
        var round = TestWorld.NewRound(Guid.NewGuid(), Guid.Empty, strokes);
        round.PlayedAt = Start.AddDays(daysFromStart);
        round.Holes = holes;
        round.TotalPutts = putts;
        round.FairwaysHit = fairways;
        round.Penalties = penalties;
        return round;
    }

    [Fact]
    public void FewerPutts_DuringTheGoal_IsAnImprovement()
    {
        var rounds = new[]
        {
            R(-30, 36), R(-20, 35), R(-10, 34), R(-5, 35), R(-2, 35), // before: 35
            R(3, 32), R(10, 31), R(20, 33), R(40, 32),                // during and the 2 weeks after: 32
            R(60, 20),                                                // too late to count
        };

        var outcome = GoalOutcome.Compute(GoalFocusArea.Putting, Start, End, rounds);

        Assert.Equal(GoalStat.PuttsPer18, outcome.Stat);
        Assert.Equal(35m, outcome.Before);
        Assert.Equal(32m, outcome.During);
        Assert.Equal(-3m, outcome.Change);
        Assert.Equal((5, 4), (outcome.RoundsBefore, outcome.RoundsDuring));
        Assert.Equal(ConfidenceLevel.High, outcome.Confidence);
        Assert.Equal(GoalVerdict.Improved, outcome.Verdict);
    }

    [Fact]
    public void OnlyTheFiveRoundsBefore_Count_AndNineHoleRoundsAreScaled()
    {
        var rounds = new[]
        {
            R(-60, 50), // older than the last five: left out
            R(-30, 34), R(-20, 34), R(-10, 34), R(-5, 34), R(-2, 17, holes: 9),
            R(3, 34), R(10, 34),
        };

        var outcome = GoalOutcome.Compute(GoalFocusArea.Putting, Start, End, rounds);

        Assert.Equal(34m, outcome.Before);
        Assert.Equal(GoalVerdict.Same, outcome.Verdict);
        Assert.Equal(ConfidenceLevel.Medium, outcome.Confidence);
    }

    [Fact]
    public void RoundsWithoutTheStat_DontCountAsZero_AndTooFewCantTellYet()
    {
        var rounds = new[] { R(-10, 34), R(-5, null), R(-2, 33), R(3, 30), R(5, null) };

        var outcome = GoalOutcome.Compute(GoalFocusArea.Putting, Start, End, rounds);

        Assert.Equal((2, 1), (outcome.RoundsBefore, outcome.RoundsDuring));
        Assert.Equal(30m, outcome.During);
        Assert.Equal(ConfidenceLevel.Low, outcome.Confidence);
        Assert.Equal(GoalVerdict.CantTellYet, outcome.Verdict);
    }

    [Fact]
    public void MoreFairways_IsBetter_AndMorePenalties_IsWorse()
    {
        var fairways = new[] { R(-5, fairways: 6), R(-2, fairways: 6), R(3, fairways: 9), R(10, fairways: 9) };
        Assert.Equal(GoalVerdict.Improved, GoalOutcome.Compute(GoalFocusArea.TeeShots, Start, End, fairways).Verdict);

        var penalties = new[] { R(-5, penalties: 1), R(-2, penalties: 1), R(3, penalties: 3), R(10, penalties: 3) };
        Assert.Equal(GoalVerdict.Worse, GoalOutcome.Compute(GoalFocusArea.Penalties, Start, End, penalties).Verdict);
    }

    [Fact]
    public void Consistency_IsTheSpreadOfScores()
    {
        var rounds = new[]
        {
            R(-10, strokes: 80), R(-5, strokes: 100), R(-2, strokes: 90), // spread 10
            R(3, strokes: 89), R(10, strokes: 91),                        // spread about 1.4
        };

        var outcome = GoalOutcome.Compute(GoalFocusArea.Consistency, Start, End, rounds);

        Assert.Equal(GoalStat.ScoreSpread, outcome.Stat);
        Assert.Equal(10m, outcome.Before);
        Assert.Equal(1.4m, outcome.During);
        Assert.Equal(GoalVerdict.Improved, outcome.Verdict);
    }

    [Fact]
    public void AGoalWithoutAFocusArea_IsntMeasured()
    {
        var outcome = GoalOutcome.Compute(null, Start, End, new[] { R(-2), R(3) });

        Assert.Null(outcome.Stat);
        Assert.Equal(GoalVerdict.NotMeasured, outcome.Verdict);
    }
}

/// <summary>Issue #234: "Did it work?" on the API, for the golfer and their coach.</summary>
public sealed class GoalOutcomeEndpointTests : IAsyncLifetime
{
    private readonly ApiFactory _factory = new();
    private readonly Guid _coachId = Guid.NewGuid();
    private readonly Guid _otherCoachId = Guid.NewGuid();
    private readonly Guid _annId = Guid.NewGuid();
    private readonly Guid _goalId = Guid.NewGuid();
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public async Task InitializeAsync() => await _factory.WithDbAsync(async db =>
    {
        db.Users.Add(TestWorld.NewUser(_coachId, "coach@example.test"));
        db.Users.Add(TestWorld.NewUser(_otherCoachId, "other@example.test"));
        db.Users.Add(TestWorld.NewUser(_annId, "ann@example.test"));
        foreach (var coach in new[] { _coachId, _otherCoachId })
            db.CoachLinks.Add(new CoachLink
            {
                CoachLinkId = Guid.NewGuid(), GolferId = _annId, CoachId = coach, ShareReflections = true,
                SharePractice = coach == _coachId, CreatedAt = DateTimeOffset.UtcNow,
            });
        db.PracticeGoals.Add(new PracticeGoal
        {
            PracticeGoalId = _goalId, GolferId = _annId, CoachId = _coachId, Title = "Lag putts",
            FocusArea = GoalFocusArea.Putting, TargetSessions = 4, StartsOn = Today.AddDays(-14), EndsOn = Today.AddDays(14),
            Status = PracticeGoalStatus.Active, CreatedAt = DateTimeOffset.UtcNow,
        });
        foreach (var (days, putts) in new[] { (-30, 36), (-20, 36), (-7, 32), (-2, 32) })
        {
            var round = TestWorld.NewRound(Guid.NewGuid(), _annId);
            round.PlayedAt = Today.AddDays(days);
            round.TotalPutts = (short)putts;
            db.Rounds.Add(round);
        }
        await db.SaveChangesAsync();
    });

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task TheGolferAndTheirCoach_SeeWhetherItWorked()
    {
        foreach (var url in new[] { $"/me/practice-goals/{_goalId}/outcome", $"/coach/students/{_annId}/practice-goals/{_goalId}/outcome" })
        {
            var client = TestWorld.ClientFor(_factory, url.StartsWith("/me") ? _annId : _coachId);
            var outcome = (await client.GetFromJsonAsync<OutcomePayload>(url))!;

            Assert.Equal("PuttsPer18", outcome.Stat);
            Assert.Equal(36m, outcome.Before);
            Assert.Equal(32m, outcome.During);
            Assert.Equal(-4m, outcome.Change);
            Assert.Equal("Medium", outcome.Confidence);
            Assert.Equal("Improved", outcome.Verdict);
            Assert.Equal(Today.AddDays(28).ToString("yyyy-MM-dd"), outcome.DuringTo);
        }
    }

    [Fact]
    public async Task WithoutAccess_ItIs404()
    {
        // Practice isn't shared with the other coach; a stranger has no link.
        Assert.Equal(HttpStatusCode.NotFound,
            (await TestWorld.ClientFor(_factory, _otherCoachId).GetAsync($"/coach/students/{_annId}/practice-goals/{_goalId}/outcome")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await TestWorld.ClientFor(_factory, _coachId).GetAsync($"/me/practice-goals/{_goalId}/outcome")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await TestWorld.ClientFor(_factory, _annId).GetAsync($"/me/practice-goals/{Guid.NewGuid()}/outcome")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _factory.CreateClient().GetAsync($"/me/practice-goals/{_goalId}/outcome")).StatusCode);
    }

    private sealed record OutcomePayload(
        string? Stat, decimal? Before, decimal? During, decimal? Change, string Confidence, string Verdict, string DuringTo);
}
