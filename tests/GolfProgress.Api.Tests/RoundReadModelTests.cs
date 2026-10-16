using GolfProgress.Api.Controllers;
using GolfProgress.Domain.Entities;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace GolfProgress.Api.Tests;

/// <summary>
/// Issue #95: what the round list and detail read. Rounds carry their score, a score
/// explains what it was judged on, and the next focus can be read without writing.
/// </summary>
public sealed class RoundReadModelTests : IAsyncLifetime
{
    private ApiFactory _factory = null!;
    private Guid _userId;

    public async Task InitializeAsync()
    {
        _factory = new ApiFactory();
        _userId = Guid.NewGuid();
        await _factory.WithDbAsync(async db =>
        {
            db.Users.Add(TestWorld.NewUser(_userId, "reader@example.test"));
            await db.SaveChangesAsync();
        });
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Rounds_CarryTheirScore_InEveryResponse()
    {
        var created = await LogAsync("2026-05-01", 92);
        Assert.NotNull(created.FitnessScore);
        Assert.Equal("Low", created.FitnessConfidence);

        var fetched = await Client().GetFromJsonAsync<RoundPayload>($"/rounds/{created.RoundId}");
        Assert.Equal(created.FitnessScore, fetched!.FitnessScore);

        await LogAsync("2026-05-08", 86);
        var list = await Client().GetFromJsonAsync<List<RoundPayload>>("/me/rounds");
        Assert.Equal(2, list!.Count);
        Assert.All(list, r => Assert.NotNull(r.FitnessScore));

        var updated = await Client().PutAsJsonAsync($"/rounds/{created.RoundId}",
            new { playedAt = "2026-05-01", holes = 18, totalStrokes = 70 });
        var body = await updated.Content.ReadFromJsonAsync<RoundPayload>();
        var rescored = await Client().GetFromJsonAsync<ScorePayload>($"/rounds/{created.RoundId}/fitness-score");
        Assert.Equal(rescored!.ScoreTotal, body!.FitnessScore);
    }

    [Fact]
    public async Task FitnessScore_ExplainsWhatItWasJudgedOn()
    {
        await LogAsync("2026-05-01", 90);
        await LogAsync("2026-05-08", 94);
        var third = await LogAsync("2026-05-15", 88);

        var score = await Client().GetFromJsonAsync<ScorePayload>($"/rounds/{third.RoundId}/fitness-score");

        // The two rounds before it.
        Assert.Equal(92m, score!.Explanation.BaselineAvgScore);
        Assert.NotNull(score.Explanation.BaselineStdDev);
        Assert.Equal(3, score.Explanation.RoundsInHabitWindow);
        Assert.Equal(0, score.Explanation.PracticeSessionsInHabitWindow);
        Assert.False(score.Explanation.Reflected);
    }

    [Fact]
    public async Task FitnessScore_ForAFirstRound_HasNoBaselineToExplain()
    {
        var first = await LogAsync("2026-05-01", 90);

        var score = await Client().GetFromJsonAsync<ScorePayload>($"/rounds/{first.RoundId}/fitness-score");

        Assert.Null(score!.Explanation.BaselineAvgScore);
        Assert.Equal(1, score.Explanation.RoundsInHabitWindow);
    }

    [Theory]
    [InlineData("{}")]                                     // computed before explanations were stored
    [InlineData("""{"avgScore":null,"habits":{}}""")]
    [InlineData("""{"avgScore":"90","habits":"x"}""")]     // not the shape expected
    public void Explanation_FromOlderScores_IsNullsRatherThanAnError(string stored)
    {
        var explanation = ProgressController.Explain(JsonDocument.Parse(stored));

        Assert.Null(explanation.BaselineAvgScore);
        Assert.Null(explanation.RoundsInHabitWindow);
        Assert.Null(explanation.Reflected);
    }

    [Fact]
    public async Task NextLever_CanBeReadWithoutLoggingAnEvent()
    {
        var round = await LogAsync("2026-05-01", 90, penalties: 3);
        var eventsBefore = EventCount("NextLeverRecommended");

        var response = await Client().GetAsync($"/rounds/{round.RoundId}/next-lever");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("penalties", (await response.Content.ReadFromJsonAsync<LeverPayload>())!.LeverKey);
        Assert.Equal(eventsBefore, EventCount("NextLeverRecommended"));

        // The POST still records that a recommendation was made.
        await Client().PostAsync($"/rounds/{round.RoundId}/next-lever", content: null);
        Assert.Equal(eventsBefore + 1, EventCount("NextLeverRecommended"));
    }

    private async Task<RoundPayload> LogAsync(string playedAt, int strokes, int? penalties = null)
    {
        var response = await Client().PostAsJsonAsync("/me/rounds",
            new { playedAt, holes = 18, totalStrokes = strokes, penalties });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RoundPayload>())!;
    }

    private int EventCount(string type)
    {
        var count = 0;
        _factory.WithDb(db => count = db.Events.Count(e => e.UserId == _userId && e.EventType == type));
        return count;
    }

    private HttpClient Client() => TestWorld.ClientFor(_factory, _userId);

    private sealed record RoundPayload(Guid RoundId, short? FitnessScore, string? FitnessConfidence);

    private sealed record ExplanationPayload(
        decimal? BaselineAvgScore, decimal? BaselineStdDev, int? RoundsInHabitWindow,
        int? PracticeSessionsInHabitWindow, bool? Reflected);

    private sealed record ScorePayload(short ScoreTotal, ExplanationPayload Explanation);

    private sealed record LeverPayload(string LeverKey);
}
