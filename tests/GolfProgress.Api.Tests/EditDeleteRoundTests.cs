using GolfProgress.Api.Services;
using GolfProgress.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Net;
using System.Net.Http.Json;

namespace GolfProgress.Api.Tests;

/// <summary>
/// Issue #81: PUT and DELETE /rounds/{id}. Editing or deleting a round rescores it,
/// the rounds it is an input to and the current baseline, in one save; deleting it
/// also removes its derived data.
/// </summary>
public sealed class EditDeleteRoundTests : IAsyncLifetime
{
    /// <summary>Strokes for 16 weekly rounds, from Monday 5 January 2026.</summary>
    private static readonly int[] Strokes = [92, 88, 95, 90, 86, 91, 89, 94, 87, 93, 85, 90, 88, 92, 86, 91];

    private static readonly DateOnly FirstDay = new(2026, 1, 5);

    private readonly FailWhenSavingEvent _failure = new();
    private ApiFactory _factory = null!;
    private Guid _userId;
    private List<Guid> _rounds = null!;

    public async Task InitializeAsync()
    {
        _factory = new ApiFactory(_failure);
        _userId = Guid.NewGuid();
        _rounds = Strokes.Select(_ => Guid.NewGuid()).ToList();

        await _factory.WithDbAsync(async db =>
        {
            db.Users.Add(TestWorld.NewUser(_userId, "editor@example.test"));

            for (var i = 0; i < Strokes.Length; i++)
            {
                var round = TestWorld.NewRound(_rounds[i], _userId, Strokes[i]);
                round.PlayedAt = DayOf(i);
                // Vary the stats so pillars other than Execution move too.
                round.TotalPutts = (short)(30 + i % 4);
                round.Penalties = (short)(i % 3);
                round.BlowUpHoles = (short)(i % 2);
                db.Rounds.Add(round);
            }

            await db.SaveChangesAsync();
        });

        // Score them as the app would have, one by one.
        await RecomputeAllAsync();
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Edit_ReturnsTheRound_AndChangesItsScoreAndTheBaseline()
    {
        var latest = _rounds[^1];
        var scoreBefore = Scores()[latest];
        var baselineBefore = await Client().GetFromJsonAsync<BaselinePayload>("/me/baseline");

        var response = await Client().PutAsJsonAsync($"/rounds/{latest}", RoundBody(DayOf(15), strokes: 75));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var round = await response.Content.ReadFromJsonAsync<RoundPayload>();
        Assert.Equal(75, round!.TotalStrokes);
        Assert.Equal(latest, round.RoundId);

        Assert.True(Scores()[latest].Execution > scoreBefore.Execution);

        var baseline = await Client().GetFromJsonAsync<BaselinePayload>("/me/baseline");
        // The five most recent rounds, with the edited one now 75.
        Assert.Equal((Strokes[11] + Strokes[12] + Strokes[13] + Strokes[14] + 75m) / 5, baseline!.AvgScore);
        Assert.NotEqual(baselineBefore!.AvgScore, baseline.AvgScore);

        _factory.WithDb(db => Assert.Single(db.Events.Where(e => e.EventType == "RoundUpdated" && e.RelatedId == latest)));
    }

    /// <summary>
    /// The heart of #81: after moving an early round to another week and changing its
    /// score, every round's score is what scoring all of them from scratch gives.
    /// </summary>
    [Fact]
    public async Task Edit_LeavesEveryScoreAsAFullRecomputeWould()
    {
        var before = Scores();

        var response = await Client().PutAsJsonAsync($"/rounds/{_rounds[3]}",
            RoundBody(new DateOnly(2026, 2, 3), strokes: 80, putts: 28));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var afterEdit = Scores();

        // Some later rounds really did change: the test isn't vacuous.
        Assert.Contains(_rounds.Skip(4), id => afterEdit[id].Total != before[id].Total);
        // A round long after both dates wasn't rescored at all.
        Assert.Equal(before[_rounds[^1]].ComputedAt, afterEdit[_rounds[^1]].ComputedAt);

        await RecomputeAllAsync();

        AssertSameScores(Scores(), afterEdit);
    }

    [Fact]
    public async Task Edit_RegeneratesTheRoundsMicroWins()
    {
        var round = _rounds[^1];
        (await Client().PostAsync($"/rounds/{round}/micro-wins/generate", content: null)).EnsureSuccessStatusCode();
        var before = await Client().GetFromJsonAsync<List<MicroWinPayload>>($"/rounds/{round}/micro-wins");

        await Client().PutAsJsonAsync($"/rounds/{round}", RoundBody(DayOf(15), strokes: 75));

        var after = await Client().GetFromJsonAsync<List<MicroWinPayload>>($"/rounds/{round}/micro-wins");
        Assert.Equal(3, after!.Count);
        Assert.Empty(after.Select(w => w.MicroWinId).Intersect(before!.Select(w => w.MicroWinId)));
    }

    [Fact]
    public async Task Edit_InvalidRound_IsRejectedAndChangesNothing()
    {
        var before = Scores();

        var response = await Client().PutAsJsonAsync($"/rounds/{_rounds[3]}",
            new { playedAt = "2026-01-26", holes = 12, totalStrokes = 80 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains(problem!.Errors.Keys, k => string.Equals(k, "Holes", StringComparison.OrdinalIgnoreCase));

        _factory.WithDb(db => Assert.Equal(Strokes[3], db.Rounds.Single(r => r.RoundId == _rounds[3]).TotalStrokes));
        AssertSameScores(Scores(), before);
    }

    [Fact]
    public async Task Edit_WhenTheSaveFails_ChangesNothing()
    {
        var before = Scores();
        _failure.EventType = "RoundUpdated";

        var response = await Client().PutAsJsonAsync($"/rounds/{_rounds[3]}", RoundBody(DayOf(3), strokes: 70));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        _factory.WithDb(db => Assert.Equal(Strokes[3], db.Rounds.Single(r => r.RoundId == _rounds[3]).TotalStrokes));
        AssertSameScores(Scores(), before, compareComputedAt: true);
    }

    [Fact]
    public async Task Delete_LeavesEveryScoreAsAFullRecomputeWould()
    {
        var response = await Client().DeleteAsync($"/rounds/{_rounds[3]}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var afterDelete = Scores();
        Assert.False(afterDelete.ContainsKey(_rounds[3]));

        await RecomputeAllAsync(except: _rounds[3]);

        AssertSameScores(Scores(), afterDelete);
    }

    [Fact]
    public async Task Delete_RemovesTheRoundAndItsDerivedData()
    {
        var round = _rounds[^1];
        (await Client().PostAsync($"/rounds/{round}/micro-wins/generate", content: null)).EnsureSuccessStatusCode();
        (await Client().PostAsJsonAsync($"/rounds/{round}/reflections", new { focusRating = 4 })).EnsureSuccessStatusCode();

        var response = await Client().DeleteAsync($"/rounds/{round}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client().GetAsync($"/rounds/{round}")).StatusCode);
        _factory.WithDb(db =>
        {
            Assert.Empty(db.Rounds.Where(r => r.RoundId == round));
            Assert.Empty(db.FitnessScores.Where(x => x.RoundId == round));
            Assert.Empty(db.MicroWins.Where(x => x.RoundId == round));
            Assert.Empty(db.Reflections.Where(x => x.RoundId == round));
            Assert.Single(db.Events.Where(e => e.EventType == "RoundDeleted" && e.RelatedId == round));
        });

        // The baseline moves back to the five rounds before it.
        var baseline = await Client().GetFromJsonAsync<BaselinePayload>("/me/baseline");
        Assert.Equal((Strokes[10] + Strokes[11] + Strokes[12] + Strokes[13] + Strokes[14]) / 5m, baseline!.AvgScore);
    }

    [Fact]
    public async Task DeletingTheLastRound_RemovesTheBaseline()
    {
        // A user with a single round.
        var (alice, _) = await TestWorld.SeedTwoUsersAsync(_factory);
        var client = TestWorld.ClientFor(_factory, alice.UserId);
        (await client.PostAsync($"/rounds/{alice.RoundId}/compute", content: null)).EnsureSuccessStatusCode();

        var response = await client.DeleteAsync($"/rounds/{alice.RoundId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        // As for a user who has never logged a round.
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/me/baseline")).StatusCode);
    }

    [Fact]
    public async Task Delete_UnknownRound_Returns404()
    {
        var response = await Client().DeleteAsync($"/rounds/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- Which rounds are rescored

    [Fact]
    public void RoundsAffectedBy_IsTheHabitWindowAndTheNextWindowOfRounds()
    {
        var rounds = Enumerable.Range(0, 16).Select(i => RoundOn(DayOf(i))).ToList();

        var affected = ProgressComputationService.RoundsAffectedBy(rounds, new[] { DayOf(3) }, windowN: 5);

        // Habits: 26 Jan to 22 Feb, rounds 3 to 6. Scoring windows: the next five
        // rounds, 4 to 8. Round 9 has five rounds since 26 Jan in its window.
        Assert.Equal(rounds.Skip(3).Take(6), affected);
    }

    [Fact]
    public void RoundsAffectedBy_IncludesRoundsTiedWithTheLastOfTheWindow()
    {
        var day = new DateOnly(2026, 6, 1);
        var later = new DateOnly(2026, 9, 1);   // past the habit window
        var rounds = new[] { RoundOn(later), RoundOn(later), RoundOn(later.AddDays(1)) };

        var affected = ProgressComputationService.RoundsAffectedBy(rounds, new[] { day }, windowN: 1);

        // Either round on 1 September could be the one in the next round's window.
        Assert.Equal(rounds.Take(2), affected);
    }

    // ---------- Helpers

    private static DateOnly DayOf(int index) => FirstDay.AddDays(7 * index);

    private static Round RoundOn(DateOnly day) => new() { RoundId = Guid.NewGuid(), PlayedAt = day };

    private static object RoundBody(DateOnly playedAt, int strokes, int putts = 30) => new
    {
        playedAt = playedAt.ToString("yyyy-MM-dd"),
        holes = 18,
        totalStrokes = strokes,
        totalPutts = putts,
        penalties = 1,
        blowUpHoles = 0
    };

    private async Task RecomputeAllAsync(Guid? except = null)
    {
        foreach (var id in _rounds.Where(id => id != except))
            (await Client().PostAsync($"/rounds/{id}/compute", content: null)).EnsureSuccessStatusCode();
    }

    private Dictionary<Guid, Score> Scores()
    {
        var scores = new Dictionary<Guid, Score>();
        _factory.WithDb(db =>
        {
            foreach (var fs in db.FitnessScores.Where(x => x.UserId == _userId))
                scores[fs.RoundId] = new Score(
                    fs.ScoreTotal, fs.Execution, fs.DamageControl, fs.Consistency, fs.Habits,
                    fs.ConfidenceLevel.ToString(), fs.ComputedAt);
        });
        return scores;
    }

    private static void AssertSameScores(
        Dictionary<Guid, Score> actual, Dictionary<Guid, Score> expected, bool compareComputedAt = false)
    {
        Assert.Equal(expected.Keys.OrderBy(k => k), actual.Keys.OrderBy(k => k));
        foreach (var (id, score) in expected)
        {
            var other = actual[id];
            Assert.Equal(compareComputedAt ? score : score with { ComputedAt = default },
                compareComputedAt ? other : other with { ComputedAt = default });
        }
    }

    private HttpClient Client() => TestWorld.ClientFor(_factory, _userId);

    private sealed record Score(
        short Total, short Execution, short DamageControl, short Consistency, short Habits,
        string Confidence, DateTimeOffset ComputedAt);

    private sealed record RoundPayload(Guid RoundId, short TotalStrokes);

    private sealed record BaselinePayload(decimal? AvgScore);

    private sealed record MicroWinPayload(Guid MicroWinId);

    /// <summary>Fails any save that adds an event of <see cref="EventType"/>.</summary>
    private sealed class FailWhenSavingEvent : SaveChangesInterceptor
    {
        public string? EventType { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (EventType is not null && eventData.Context!.ChangeTracker.Entries<EventLog>()
                    .Any(e => e.State == EntityState.Added && e.Entity.EventType == EventType))
                throw new InvalidOperationException($"Simulated failure while saving {EventType}.");

            return base.SavingChangesAsync(eventData, result, ct);
        }
    }
}
