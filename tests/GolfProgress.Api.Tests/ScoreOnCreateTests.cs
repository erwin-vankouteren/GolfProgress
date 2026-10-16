using GolfProgress.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Net;
using System.Net.Http.Json;

namespace GolfProgress.Api.Tests;

/// <summary>
/// Issue #80. Logging a round scores it in the same request: a client shows the
/// score after one write, and a round is never left unscored because the app closed
/// between calls.
/// </summary>
public sealed class ScoreOnCreateTests : IAsyncLifetime
{
    private readonly FailWhenSavingMicroWins _failure = new();
    private ApiFactory _factory = null!;
    private Guid _userId;

    /// <summary>
    /// Five earlier rounds of varying scores, 20 days apart (May 1 to July 20), so the
    /// baseline and Consistency have something to work with. Only two of them fall in
    /// the 28-day habit window before the logged round (July 25), so Habits only
    /// comes out right if the round being logged counts as well: 3 of 4 activities.
    /// </summary>
    public async Task InitializeAsync()
    {
        _factory = new ApiFactory(_failure);
        _userId = Guid.NewGuid();

        await _factory.WithDbAsync(async db =>
        {
            db.Users.Add(TestWorld.NewUser(_userId, "logger@example.test"));

            int[] strokes = [92, 88, 95, 90, 86];
            for (var i = 0; i < strokes.Length; i++)
            {
                var round = TestWorld.NewRound(Guid.NewGuid(), _userId, strokes[i]);
                round.PlayedAt = new DateOnly(2026, 5, 1).AddDays(i * 20);
                db.Rounds.Add(round);
            }

            await db.SaveChangesAsync();
        });

        // Seeding saves through the interceptor too; count only the request.
        _failure.Reset();
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task LoggedRound_HasScoreBaselineAndMicroWins_WithoutFurtherCalls()
    {
        var client = TestWorld.ClientFor(_factory, _userId);

        var roundId = await LogRoundAsync(client);

        var score = await client.GetFromJsonAsync<ScorePayload>($"/rounds/{roundId}/fitness-score");
        // Three rounds in the habit window, counting this one: 3 * 15 / 4, no reflection.
        Assert.Equal(11, score!.Habits);

        var wins = await client.GetFromJsonAsync<List<MicroWinPayload>>($"/rounds/{roundId}/micro-wins");
        Assert.Equal(3, wins!.Count);

        var baseline = await client.GetFromJsonAsync<BaselinePayload>("/me/baseline");
        // The current baseline is the five most recent rounds, including the one just
        // logged; the oldest (92) drops out of the window.
        Assert.Equal((88m + 95 + 90 + 86 + 84) / 5, baseline!.AvgScore);

        // The round, its score, the baseline, micro-wins and every event: one save.
        Assert.Equal(1, _failure.Saves);
    }

    [Fact]
    public async Task ScoreFromLogging_MatchesRecomputingTheSavedRound()
    {
        var client = TestWorld.ClientFor(_factory, _userId);
        var roundId = await LogRoundAsync(client);

        var onCreate = await client.GetFromJsonAsync<ScorePayload>($"/rounds/{roundId}/fitness-score");
        var baselineOnCreate = await client.GetFromJsonAsync<BaselinePayload>("/me/baseline");

        // Recompute once the round is in the database, as the existing endpoint does.
        // Scoring a round that is still being added must give the same answer.
        (await client.PostAsync($"/rounds/{roundId}/compute", content: null)).EnsureSuccessStatusCode();

        var recomputed = await client.GetFromJsonAsync<ScorePayload>($"/rounds/{roundId}/fitness-score");
        var baselineRecomputed = await client.GetFromJsonAsync<BaselinePayload>("/me/baseline");

        Assert.Equal(recomputed, onCreate);
        Assert.Equal(baselineRecomputed, baselineOnCreate);
    }

    [Fact]
    public async Task LoggingARound_WhenTheSaveFails_WritesNothing()
    {
        _failure.Enabled = true;
        var client = TestWorld.ClientFor(_factory, _userId);

        var response = await client.PostAsJsonAsync("/me/rounds", NewRoundRequest());

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        _factory.WithDb(db =>
        {
            Assert.Equal(5, db.Rounds.Count(r => r.UserId == _userId));
            Assert.Empty(db.FitnessScores.Where(fs => fs.UserId == _userId));
            Assert.Empty(db.UserBaselines.Where(b => b.UserId == _userId));
            Assert.Empty(db.Events.Where(e => e.UserId == _userId));
        });
    }

    private static object NewRoundRequest() => new
    {
        playedAt = "2026-07-25",
        holes = 18,
        totalStrokes = 84,
        totalPutts = 30,
        penalties = 1,
        threePutts = 0,
        blowUpHoles = 0
    };

    private static async Task<Guid> LogRoundAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/me/rounds", NewRoundRequest());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var round = await response.Content.ReadFromJsonAsync<RoundPayload>();
        return round!.RoundId;
    }

    private sealed record RoundPayload(Guid RoundId);

    private sealed record ScorePayload(
        short ScoreTotal, short Execution, short DamageControl, short Consistency, short Habits,
        string ConfidenceLevel);

    private sealed record BaselinePayload(
        decimal? AvgScore, decimal? ScoreStdDev, decimal? AvgPutts, decimal? AvgPenalties,
        decimal CompletenessRatio, string ConfidenceLevel);

    private sealed record MicroWinPayload(string Category, string Title);

    /// <summary>
    /// Fails any save that includes micro-wins, the last thing logging a round stages.
    /// Also counts saves, so a test can see how many there were.
    /// </summary>
    private sealed class FailWhenSavingMicroWins : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }
        public int Saves { get; private set; }

        public void Reset() => Saves = 0;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
        {
            Saves++;

            var hasMicroWins = eventData.Context!.ChangeTracker.Entries<MicroWin>()
                .Any(e => e.State == EntityState.Added);

            if (Enabled && hasMicroWins)
                throw new InvalidOperationException("Simulated failure while saving micro-wins.");

            return base.SavingChangesAsync(eventData, result, ct);
        }
    }
}
