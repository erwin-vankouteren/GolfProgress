using System.Net;
using System.Net.Http.Json;

namespace GolfProgress.Api.Tests;

/// <summary>
/// Covers GET /me, and pins the removal of the two UsersController endpoints:
/// the anonymous passwordless POST /users (issue #3) and the role-gated
/// GET /users/{id} that answered 403 to everyone (issue #4).
/// </summary>
public sealed class UserProfileTests : IAsyncLifetime
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
    public async Task GetMe_ReturnsTheCallersOwnProfile()
    {
        var client = TestWorld.ClientFor(_factory, _alice.UserId);

        var response = await client.GetAsync("/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var profile = await response.Content.ReadFromJsonAsync<UserPayload>();
        Assert.Equal(_alice.UserId, profile!.UserId);
        Assert.Equal("alice@example.test", profile.Email);
        Assert.Equal("alice", profile.DisplayName);
        Assert.Equal("Metric", profile.Units);
        Assert.Equal("Minimal", profile.TrackingMode);
    }

    /// <summary>
    /// The endpoint resolves the user from the token, so two callers hitting the
    /// same URL must receive different records. There is no id to tamper with.
    /// </summary>
    [Fact]
    public async Task GetMe_IsScopedToTheTokenNotAnId()
    {
        var aliceProfile = await TestWorld.ClientFor(_factory, _alice.UserId)
            .GetFromJsonAsync<UserPayload>("/me");

        var bobProfile = await TestWorld.ClientFor(_factory, _bob.UserId)
            .GetFromJsonAsync<UserPayload>("/me");

        Assert.Equal(_alice.UserId, aliceProfile!.UserId);
        Assert.Equal(_bob.UserId, bobProfile!.UserId);
        Assert.NotEqual(aliceProfile.UserId, bobProfile.UserId);
        Assert.NotEqual(aliceProfile.Email, bobProfile.Email);
    }

    [Fact]
    public async Task GetMe_RequiresAuthentication()
    {
        var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync("/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_ForATokenWhoseUserNoLongerExists_Returns404()
    {
        // A structurally valid token for a user that was never seeded. The route
        // must not fall over, and must not leak anyone else's record.
        var client = TestWorld.ClientFor(_factory, Guid.NewGuid());

        var response = await client.GetAsync("/me");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Issue #3: POST /users was anonymous and created users with no password,
    /// which could never log in. Registration belongs to /auth/register alone.
    /// </summary>
    [Fact]
    public async Task PostUsers_NoLongerExists_Issue3()
    {
        var anonymous = _factory.CreateClient();

        var response = await anonymous.PostAsJsonAsync(
            "/users", new { displayName = "unauthenticated signup" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // And nothing was created.
        _factory.WithDb(db =>
            Assert.Equal(2, db.Users.Count()));
    }

    /// <summary>Issue #4: the role-gated lookup is gone, replaced by GET /me.</summary>
    [Fact]
    public async Task GetUserById_NoLongerExists_Issue4()
    {
        var client = TestWorld.ClientFor(_factory, _alice.UserId);

        var own = await client.GetAsync($"/users/{_alice.UserId}");
        var other = await client.GetAsync($"/users/{_bob.UserId}");

        Assert.Equal(HttpStatusCode.NotFound, own.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
    }

    [Fact]
    public async Task Register_StillCreatesUsers_AndTheyCanReadTheirProfile()
    {
        var anonymous = _factory.CreateClient();

        var register = await anonymous.PostAsJsonAsync("/auth/register", new
        {
            email = "carol@example.test",
            password = "correct horse battery staple",
            displayName = "carol"
        });

        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        var auth = await register.Content.ReadFromJsonAsync<AuthPayload>();
        Assert.NotNull(auth!.Token);

        // Use the token the API itself issued, rather than one minted by the test
        // helper, so registration and /me are verified end to end.
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.Token);

        var profile = await client.GetFromJsonAsync<UserPayload>("/me");

        Assert.Equal(auth.UserId, profile!.UserId);
        Assert.Equal("carol@example.test", profile.Email);
    }

    private sealed record UserPayload(
        Guid UserId, string? Email, string? DisplayName, string? Timezone,
        string Units, string TrackingMode, decimal? HandicapIndex, int? Typical18Score,
        List<string> GoalFocus, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

    private sealed record AuthPayload(Guid UserId, string Token, string RefreshToken);
}
