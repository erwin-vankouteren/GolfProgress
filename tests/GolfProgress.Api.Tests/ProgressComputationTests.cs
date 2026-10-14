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

    [Fact]
    public async Task Compute_PersistsBaselineFromThePriorRoundsOnly()
    {
        var client = TestWorld.ClientFor(_factory, _userId);

        var compute = await client.PostAsync($"/rounds/{_targetRoundId}/compute", content: null);
        Assert.Equal(HttpStatusCode.NoContent, compute.StatusCode);

        var baseline = await client.GetFromJsonAsync<BaselinePayload>("/me/baseline");

        // Mean of 100, 98, 96, 94, 92 -- the target round's own 90 is excluded.
        Assert.Equal(96m, baseline!.AvgScore);
        Assert.InRange(baseline.ScoreStdDev!.Value, 3.16m, 3.17m);
        Assert.Equal(5, baseline.WindowRounds);

        // 2 of 6 metrics tracked per round.
        Assert.InRange(baseline.CompletenessRatio, 0.33m, 0.34m);

        // 5 samples clears the Medium threshold of 4, but completeness is below
        // the 0.5 that High additionally requires.
        Assert.Equal(nameof(ConfidenceLevel.Medium), baseline.ConfidenceLevel);
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
    /// Documents CURRENT behaviour, which is wrong. See issue #9.
    /// UserBaselines holds one row per user, so recomputing an OLDER round
    /// overwrites the baseline with that round's narrower window. After this
    /// sequence the stored baseline no longer describes the user's latest form.
    /// Update this test when #9 is fixed.
    /// </summary>
    [Fact]
    public async Task Compute_OnAnOlderRound_OverwritesTheBaseline_Issue9()
    {
        var client = TestWorld.ClientFor(_factory, _userId);

        await client.PostAsync($"/rounds/{_targetRoundId}/compute", content: null);
        var afterLatest = await client.GetFromJsonAsync<BaselinePayload>("/me/baseline");
        Assert.Equal(96m, afterLatest!.AvgScore);

        // Recompute the second-oldest round. Its window is the single round before
        // it (100 strokes), so the user's baseline average jumps from 96 to 100.
        Guid olderRoundId = default;
        _factory.WithDb(db =>
            olderRoundId = db.Rounds
                .Where(r => r.UserId == _userId && r.PlayedAt == new DateOnly(2026, 5, 2))
                .Select(r => r.RoundId)
                .Single());

        await client.PostAsync($"/rounds/{olderRoundId}/compute", content: null);

        var afterOlder = await client.GetFromJsonAsync<BaselinePayload>("/me/baseline");
        Assert.Equal(100m, afterOlder!.AvgScore);
        Assert.NotEqual(afterLatest.AvgScore, afterOlder.AvgScore);
    }

    private sealed record BaselinePayload(
        Guid UserId, short WindowRounds, decimal? AvgScore, decimal? ScoreStdDev,
        decimal CompletenessRatio, string ConfidenceLevel);

    private sealed record FitnessScorePayload(
        Guid UserId, Guid RoundId, short ScoreTotal,
        short Execution, short DamageControl, short Consistency, short Habits,
        string ConfidenceLevel);
}
