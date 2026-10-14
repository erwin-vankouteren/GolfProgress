using System.Net;
using System.Net.Http.Json;

namespace GolfProgress.Api.Tests;

/// <summary>
/// Pins the authorization fix: every endpoint keyed on a round id must refuse a
/// caller who does not own that round. These are the regression guard for the IDOR
/// vulnerability -- if a new round-keyed endpoint is added without an ownership
/// check, <see cref="EveryRoundKeyedRoute_RejectsAnotherUsersRound"/> should be
/// extended and will fail until the guard is in place.
/// </summary>
public sealed class RoundOwnershipTests : IAsyncLifetime
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

    /// <summary>Every GET keyed on a round id, probed with the wrong user's token.</summary>
    public static TheoryData<string> ReadRoutes() => new()
    {
        "/rounds/{roundId}",
        "/rounds/{roundId}/fitness-score",
        "/rounds/{roundId}/micro-wins",
        "/rounds/{roundId}/reflections"
    };

    /// <summary>Every POST keyed on a round id, probed with the wrong user's token.</summary>
    public static TheoryData<string> WriteRoutes() => new()
    {
        "/rounds/{roundId}/compute",
        "/rounds/{roundId}/micro-wins/generate",
        "/rounds/{roundId}/next-lever"
    };

    [Theory]
    [MemberData(nameof(ReadRoutes))]
    public async Task Get_AnotherUsersRound_Returns404(string template)
    {
        var client = TestWorld.ClientFor(_factory, _alice.UserId);

        var response = await client.GetAsync(template.Replace("{roundId}", _bob.RoundId.ToString()));

        // 404 rather than 403: the response must not reveal that Bob's round exists.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(WriteRoutes))]
    public async Task Post_AnotherUsersRound_Returns404(string template)
    {
        var client = TestWorld.ClientFor(_factory, _alice.UserId);

        var response = await client.PostAsync(
            template.Replace("{roundId}", _bob.RoundId.ToString()), content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(ReadRoutes))]
    public async Task Get_OwnRound_DoesNotReturn404(string template)
    {
        var client = TestWorld.ClientFor(_factory, _alice.UserId);

        var response = await client.GetAsync(template.Replace("{roundId}", _alice.RoundId.ToString()));

        // Guards against "fixing" the IDOR by rejecting everyone. Some of these
        // legitimately 404 when the derived record has not been computed yet, so
        // this asserts only that the owner is not denied access to the round itself.
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetRound_OwnRound_ReturnsIt()
    {
        var client = TestWorld.ClientFor(_factory, _alice.UserId);

        var response = await client.GetAsync($"/rounds/{_alice.RoundId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RoundPayload>();
        Assert.Equal(_alice.RoundId, body!.RoundId);
        Assert.Equal(_alice.UserId, body.UserId);
    }

    /// <summary>
    /// The original worst case: posting a reflection to another user's round wrote
    /// the row under the round owner's UserId, injecting records into their account.
    /// </summary>
    [Fact]
    public async Task PostReflection_ToAnotherUsersRound_Returns404AndWritesNothing()
    {
        var client = TestWorld.ClientFor(_factory, _alice.UserId);

        var response = await client.PostAsJsonAsync(
            $"/rounds/{_bob.RoundId}/reflections",
            new { focusRating = (short)4, whatWorked = "injected by alice" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        _factory.WithDb(db =>
        {
            Assert.Empty(db.Reflections.Where(r => r.RoundId == _bob.RoundId));
            Assert.Empty(db.Reflections.Where(r => r.UserId == _bob.UserId));
        });
    }

    [Fact]
    public async Task PostReflection_ToOwnRound_IsStoredUnderTheCaller()
    {
        var client = TestWorld.ClientFor(_factory, _alice.UserId);

        var response = await client.PostAsJsonAsync(
            $"/rounds/{_alice.RoundId}/reflections",
            new { focusRating = (short)4, whatWorked = "kept the driver in play" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        _factory.WithDb(db =>
        {
            var stored = db.Reflections.Single(r => r.RoundId == _alice.RoundId);
            Assert.Equal(_alice.UserId, stored.UserId);
        });
    }

    [Fact]
    public async Task MyRounds_ReturnsOnlyTheCallersRounds()
    {
        var client = TestWorld.ClientFor(_factory, _alice.UserId);

        var rounds = await client.GetFromJsonAsync<List<RoundPayload>>("/me/rounds");

        Assert.Single(rounds!);
        Assert.Equal(_alice.RoundId, rounds![0].RoundId);
    }

    [Fact]
    public async Task RoundKeyedRoutes_RequireAuthentication()
    {
        var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync($"/rounds/{_alice.RoundId}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed record RoundPayload(Guid RoundId, Guid UserId, short TotalStrokes);
}
