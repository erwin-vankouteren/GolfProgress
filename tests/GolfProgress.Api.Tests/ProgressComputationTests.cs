using GolfProgress.Domain.Enums;
using System.Net;
using System.Net.Http.Json;

namespace GolfProgress.Api.Tests;

/// <summary>
/// Drives POST /rounds/{id}/compute through the real pipeline and checks what it
/// persists, so the baseline window and the pillar arithmetic are pinned end to end
/// and not just at the pure-function level.
/// </summary>
public sealed class ProgressComputationTests : IAsyncLifetime
{
    private ApiFactory _factory = null!;
    private Guid _userId;
    private Guid _targetRoundId;

    /// <summary>
    /// Six rounds on consecutive days. The five earlier ones form the baseline
    /// window for the sixth, which is the round under test.
    ///
    /// Priors: 100, 98, 96, 94, 92  ->  mean 96, sample std dev sqrt(10) = 3.162
    /// Target: 90
    /// </summary>
    public async Task InitializeAsync()
    {
        _factory = new ApiFactory();
        _userId = Guid.NewGuid();
        _targetRoundId = Guid.NewGuid();

        await _factory.WithDbAsync(async db =>
        {
            db.Users.Add(TestWorld.NewUser(_userId, "scoring@example.test"));

            var strokes = new[] { 100, 98, 96, 94, 92 };
            for (var i = 0; i < strokes.Length; i++)
            {
                var round = TestWorld.NewRound(Guid.NewGuid(), _userId, strokes[i]);
                round.PlayedAt = new DateOnly(2026, 5, 1).AddDays(i);
                // Only penalties and blow-ups tracked: 2 of 6 metrics per round.
                round.TotalPutts = null;
                round.ThreePutts = null;
                round.Penalties = 0;
                round.BlowUpHoles = 0;
                db.Rounds.Add(round);
            }

            var target = TestWorld.NewRound(_targetRoundId, _userId, 90);
            target.PlayedAt = new DateOnly(2026, 5, 6);
            target.TotalPutts = null;
            target.ThreePutts = null;
            target.Penalties = 0;
            target.BlowUpHoles = 0;
            db.Rounds.Add(target);

            await db.SaveChangesAsync();
        });
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// The persisted baseline describes the user's CURRENT form: their most recent
    /// N rounds, including the latest. That is deliberately not the same window the
    /// round was scored against -- see
    /// <see cref="Compute_ScoresTheRoundAgainstItsOwnPriorWindow"/>.
    /// </summary>
    [Fact]
    public async Task Compute_PersistsBaselineFromTheMostRecentRounds()
    {
        var client = TestWorld.ClientFor(_factory, _userId);

        var compute = await client.PostAsync($"/rounds/{_targetRoundId}/compute", content: null);
        Assert.Equal(HttpStatusCode.NoContent, compute.StatusCode);

        var baseline = await client.GetFromJsonAsync<BaselinePayload>("/me/baseline");

        // Five most recent of 100/98/96/94/92/90 is 98,96,94,92,90 -> mean 94.
        // The oldest round (100) falls outside the window.
        Assert.Equal(94m, baseline!.AvgScore);
        Assert.InRange(baseline.ScoreStdDev!.Value, 3.16m, 3.17m);
        Assert.Equal(5, baseline.WindowRounds);

        // 2 of 6 metrics tracked per round.
        Assert.InRange(baseline.CompletenessRatio, 0.33m, 0.34m);

        // 5 samples clears the Medium threshold of 4, but completeness is below
        // the 0.5 that High additionally requires.
        Assert.Equal(nameof(ConfidenceLevel.Medium), baseline.ConfidenceLevel);
    }

    /// <summary>
    /// The round is judged against the rounds played BEFORE it (mean 96), not
    /// against the current baseline (mean 94). Keeping these separate is what
    /// allows the stored baseline to be recomputed freely without changing any
    /// round's score.
    /// </summary>
    [Fact]
    public async Task Compute_ScoresTheRoundAgainstItsOwnPriorWindow()
    {
        var client = TestWorld.ClientFor(_factory, _userId);

        await client.PostAsync($"/rounds/{_targetRoundId}/compute", content: null);

        var score = await client.GetFromJsonAsync<FitnessScorePayload>(
            $"/rounds/{_targetRoundId}/fitness-score");
        var baseline = await client.GetFromJsonAsync<BaselinePayload>("/me/baseline");

        // Execution 18 implies a scoring baseline of 96: 12 - round(90 - 96).
        // Had it used the current baseline of 94 it would have been 16.
        Assert.Equal(18, score!.Execution);
        Assert.Equal(94m, baseline!.AvgScore);
    }

    [Fact]
    public async Task Compute_PersistsFitnessScoreWithTheExpectedPillars()
    {
        var client = TestWorld.ClientFor(_factory, _userId);

        await client.PostAsync($"/rounds/{_targetRoundId}/compute", content: null);

        var score = await client.GetFromJsonAsync<FitnessScorePayload>(
            $"/rounds/{_targetRoundId}/fitness-score");

        // execution: 12 - round(90 - 96) = 18
        Assert.Equal(18, score!.Execution);

        // damage: 25 - 0 penalties - 0 blow-ups = 25
        Assert.Equal(25, score.DamageControl);

        // consistency: |90 - 96| = 6, outside one std dev of 3.162, so 8
        Assert.Equal(8, score.Consistency);

        // habits: fixed placeholder
        Assert.Equal(10, score.Habits);

        Assert.Equal(61, score.ScoreTotal);
        Assert.Equal(_targetRoundId, score.RoundId);
        Assert.Equal(_userId, score.UserId);
    }

    [Fact]
    public async Task Compute_IsIdempotent()
    {
        var client = TestWorld.ClientFor(_factory, _userId);

        await client.PostAsync($"/rounds/{_targetRoundId}/compute", content: null);
        var first = await client.GetFromJsonAsync<FitnessScorePayload>(
            $"/rounds/{_targetRoundId}/fitness-score");

        await client.PostAsync($"/rounds/{_targetRoundId}/compute", content: null);
        var second = await client.GetFromJsonAsync<FitnessScorePayload>(
            $"/rounds/{_targetRoundId}/fitness-score");

        Assert.Equal(first!.ScoreTotal, second!.ScoreTotal);

        // One score row per round, not one per compute call.
        _factory.WithDb(db =>
            Assert.Single(db.FitnessScores.Where(fs => fs.RoundId == _targetRoundId)));
    }

    [Fact]
    public async Task Compute_EmitsBaselineAndFitnessEvents()
    {
        var client = TestWorld.ClientFor(_factory, _userId);

        await client.PostAsync($"/rounds/{_targetRoundId}/compute", content: null);

        _factory.WithDb(db =>
        {
            var types = db.Events
                .Where(e => e.UserId == _userId && e.RelatedId == _targetRoundId)
                .Select(e => e.EventType)
                .ToList();

            Assert.Contains("BaselineComputed", types);
            Assert.Contains("FitnessScoreComputed", types);
        });
    }

    /// <summary>
    /// Regression guard for issue #9. Recomputing an older round used to overwrite
    /// the user's single baseline row with that round's narrower window, dropping
    /// the average from 96 to 100 -- the baseline no longer described current form.
    /// The stored baseline is now recomputed from the most recent rounds regardless
    /// of which round was scored, so it is unaffected.
    /// </summary>
    [Fact]
    public async Task Compute_OnAnOlderRound_LeavesTheBaselineCorrect_Issue9()
    {
        var client = TestWorld.ClientFor(_factory, _userId);

        await client.PostAsync($"/rounds/{_targetRoundId}/compute", content: null);
        var afterLatest = await client.GetFromJsonAsync<BaselinePayload>("/me/baseline");
        Assert.Equal(94m, afterLatest!.AvgScore);

        // The second-oldest round. Its own scoring window is the single round
        // before it (100 strokes), which previously leaked into the stored baseline.
        var olderRoundId = OldestRoundOn(new DateOnly(2026, 5, 2));

        await client.PostAsync($"/rounds/{olderRoundId}/compute", content: null);

        var afterOlder = await client.GetFromJsonAsync<BaselinePayload>("/me/baseline");
        Assert.Equal(94m, afterOlder!.AvgScore);
        Assert.Equal(afterLatest.AvgScore, afterOlder.AvgScore);
    }

    /// <summary>
    /// The stored baseline must not depend on the order rounds happen to be
    /// computed in. Computing every round oldest-first and newest-first must leave
    /// the same value.
    /// </summary>
    [Fact]
    public async Task Compute_InAnyOrder_ConvergesOnTheSameBaseline_Issue9()
    {
        var client = TestWorld.ClientFor(_factory, _userId);

        List<Guid> roundIds = null!;
        _factory.WithDb(db =>
            roundIds = db.Rounds
                .Where(r => r.UserId == _userId)
                .OrderBy(r => r.PlayedAt)
                .Select(r => r.RoundId)
                .ToList());

        foreach (var id in roundIds)
            await client.PostAsync($"/rounds/{id}/compute", content: null);

        var oldestFirst = await client.GetFromJsonAsync<BaselinePayload>("/me/baseline");

        foreach (var id in Enumerable.Reverse(roundIds))
            await client.PostAsync($"/rounds/{id}/compute", content: null);

        var newestFirst = await client.GetFromJsonAsync<BaselinePayload>("/me/baseline");

        Assert.Equal(94m, oldestFirst!.AvgScore);
        Assert.Equal(oldestFirst.AvgScore, newestFirst!.AvgScore);
        Assert.Equal(oldestFirst.ScoreStdDev, newestFirst.ScoreStdDev);
        Assert.Equal(oldestFirst.ConfidenceLevel, newestFirst.ConfidenceLevel);
    }

    /// <summary>
    /// Each round keeps its own score, computed against its own prior window, even
    /// after other rounds have been recomputed.
    /// </summary>
    [Fact]
    public async Task Compute_OnAnOlderRound_DoesNotDisturbTheLatestRoundsScore_Issue9()
    {
        var client = TestWorld.ClientFor(_factory, _userId);

        await client.PostAsync($"/rounds/{_targetRoundId}/compute", content: null);
        var before = await client.GetFromJsonAsync<FitnessScorePayload>(
            $"/rounds/{_targetRoundId}/fitness-score");

        await client.PostAsync($"/rounds/{OldestRoundOn(new DateOnly(2026, 5, 2))}/compute", content: null);

        var after = await client.GetFromJsonAsync<FitnessScorePayload>(
            $"/rounds/{_targetRoundId}/fitness-score");

        Assert.Equal(before!.ScoreTotal, after!.ScoreTotal);
        Assert.Equal(before.Execution, after.Execution);
    }

    private Guid OldestRoundOn(DateOnly playedAt)
    {
        Guid id = default;
        _factory.WithDb(db =>
            id = db.Rounds
                .Where(r => r.UserId == _userId && r.PlayedAt == playedAt)
                .Select(r => r.RoundId)
                .Single());
        return id;
    }

    private sealed record BaselinePayload(
        Guid UserId, short WindowRounds, decimal? AvgScore, decimal? ScoreStdDev,
        decimal CompletenessRatio, string ConfidenceLevel);

    private sealed record FitnessScorePayload(
        Guid UserId, Guid RoundId, short ScoreTotal,
        short Execution, short DamageControl, short Consistency, short Habits,
        string ConfidenceLevel);
}
