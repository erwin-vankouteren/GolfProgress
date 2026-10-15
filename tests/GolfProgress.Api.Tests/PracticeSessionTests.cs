using GolfProgress.Api.Services;
using System.Net;
using System.Net.Http.Json;

namespace GolfProgress.Api.Tests;

/// <summary>
/// Regression guard for issue #19: practice sessions can be logged, listed, read
/// and deleted by their owner only, and count towards the Habits pillar.
/// </summary>
public sealed class PracticeSessionTests : IAsyncLifetime
{
    private ApiFactory _factory = null!;
    private Owner _alice = null!;
    private Owner _bob = null!;

    public async Task InitializeAsync()
    {
        _factory = new ApiFactory();
        (_alice, _bob) = await TestWorld.SeedTwoUsersAsync(_factory);
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Create_ThenGet_RoundTripsTheSession()
    {
        var client = TestWorld.ClientFor(_factory, _alice.UserId);

        var created = await client.PostAsJsonAsync("/me/practice-sessions", new
        {
            practicedAt = "2026-04-28",
            durationMinutes = 60,
            sessionType = "putting",
            venueName = "Home club",
            tags = new[] { "lag putting", " gate drill " },
            primaryGoal = "Speed control"
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await created.Content.ReadFromJsonAsync<SessionPayload>();
        Assert.Equal("Putting", body!.SessionType);
        Assert.Equal(new[] { "lag putting", "gate drill" }, body.Tags);

        var fetched = await client.GetFromJsonAsync<SessionPayload>(created.Headers.Location!);
        Assert.Equal(body.PracticeSessionId, fetched!.PracticeSessionId);
        Assert.Equal("2026-04-28", fetched.PracticedAt);
        Assert.Equal(_alice.UserId, fetched.UserId);
    }

    [Fact]
    public async Task List_IsNewestFirst_PagedAndOnlyTheCallers()
    {
        var alice = TestWorld.ClientFor(_factory, _alice.UserId);
        foreach (var day in new[] { "2026-04-01", "2026-04-03", "2026-04-02" })
            await LogAsync(alice, day);
        await LogAsync(TestWorld.ClientFor(_factory, _bob.UserId), "2026-04-05");

        var response = await alice.GetAsync("/me/practice-sessions?take=2");
        var page = await response.Content.ReadFromJsonAsync<List<SessionPayload>>();

        Assert.Equal("3", response.Headers.GetValues("X-Total-Count").Single());
        Assert.Equal(new[] { "2026-04-03", "2026-04-02" }, page!.Select(s => s.PracticedAt));
    }

    [Fact]
    public async Task AnotherUsersSession_Is404ForGetAndDelete_AndIsNotDeleted()
    {
        var bobsSession = await LogAsync(TestWorld.ClientFor(_factory, _bob.UserId), "2026-04-05");
        var alice = TestWorld.ClientFor(_factory, _alice.UserId);

        Assert.Equal(HttpStatusCode.NotFound,
            (await alice.GetAsync($"/me/practice-sessions/{bobsSession}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await alice.DeleteAsync($"/me/practice-sessions/{bobsSession}")).StatusCode);

        _factory.WithDb(db => Assert.Single(db.PracticeSessions.Where(s => s.PracticeSessionId == bobsSession)));
    }

    [Fact]
    public async Task Delete_OwnSession_RemovesIt()
    {
        var client = TestWorld.ClientFor(_factory, _alice.UserId);
        var id = await LogAsync(client, "2026-04-05");

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/me/practice-sessions/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/me/practice-sessions/{id}")).StatusCode);
    }

    [Theory]
    [InlineData("practicedAt", "not-a-date")]
    [InlineData("sessionType", "Bowling")]
    [InlineData("durationMinutes", 0)]
    [InlineData("durationMinutes", 1441)]
    public async Task Create_InvalidField_IsRejected(string field, object value)
    {
        var body = new Dictionary<string, object>
        {
            ["practicedAt"] = "2026-04-05",
            ["sessionType"] = "Range",
            [field] = value
        };

        var response = await TestWorld.ClientFor(_factory, _alice.UserId)
            .PostAsJsonAsync("/me/practice-sessions", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        _factory.WithDb(db => Assert.Empty(db.PracticeSessions));
    }

    /// <summary>
    /// Practice in the 28 days up to a round counts towards Habits alongside rounds.
    /// The seeded round (2026-05-01) alone earns 3; three practice sessions in its
    /// window bring the activity count to 4 and the frequency points to 15. A session
    /// after the round's date does not count.
    /// </summary>
    [Fact]
    public async Task PracticeInTheWindow_RaisesHabitsOnCompute()
    {
        var client = TestWorld.ClientFor(_factory, _alice.UserId);

        foreach (var day in new[] { "2026-04-10", "2026-04-20", "2026-04-30", "2026-05-02" })
            await LogAsync(client, day);

        await client.PostAsync($"/rounds/{_alice.RoundId}/compute", content: null);
        var score = await client.GetFromJsonAsync<ScorePayload>($"/rounds/{_alice.RoundId}/fitness-score");

        Assert.Equal(15, score!.Habits);
    }

    [Theory]
    // rounds, practice sessions, expected habits (no reflection)
    [InlineData(1, 0, 3)]
    [InlineData(1, 3, 15)]
    [InlineData(0, 4, 15)]
    [InlineData(2, 9, 15)]   // shares the cap with rounds: still at most 15
    public void Habits_CountPracticeAlongsideRounds(int rounds, int practice, int expected)
        => Assert.Equal(expected,
            ProgressComputationService.HabitsScore(new HabitSignals(rounds, Reflected: false, practice)));

    private static async Task<Guid> LogAsync(HttpClient client, string practicedAt)
    {
        var response = await client.PostAsJsonAsync("/me/practice-sessions",
            new { practicedAt, sessionType = "Range" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SessionPayload>())!.PracticeSessionId;
    }

    private sealed record SessionPayload(
        Guid PracticeSessionId, Guid UserId, string PracticedAt, string SessionType, List<string> Tags);

    private sealed record ScorePayload(short Habits);
}
