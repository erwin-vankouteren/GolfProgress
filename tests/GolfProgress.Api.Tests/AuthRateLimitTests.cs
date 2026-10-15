using System.Net;
using System.Net.Http.Json;

namespace GolfProgress.Api.Tests;

/// <summary>
/// Regression guard for issue #6: the anonymous /auth/* endpoints are throttled per
/// client IP, and login is additionally throttled per email address.
///
/// Uses the default limits: 10 requests per IP per minute across /auth/*, and 5
/// login attempts per email per five minutes. Each test gets its own factory, so its
/// own limiter state. Under TestServer every request shares one (unknown) client IP.
/// </summary>
public sealed class AuthRateLimitTests : IDisposable
{
    private const int PerIpLimit = 10;
    private const int PerEmailLimit = 5;

    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task AuthEndpoints_FromOneIp_AreLimitedAcrossAllOfThem()
    {
        var client = _factory.CreateClient();

        // Spread over distinct emails and endpoints, so only the per-IP limit applies.
        for (var i = 0; i < PerIpLimit; i++)
        {
            var response = i % 2 == 0
                ? await LoginAsync(client, $"user{i}@example.test", "wrong")
                : await client.PostAsJsonAsync("/auth/refresh", new { refreshToken = "not-a-token" });

            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        var limited = await client.PostAsJsonAsync("/auth/register",
            new { email = "late@example.test", displayName = "late", password = "Passw0rd!" });

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.RetryAfter?.Delta > TimeSpan.Zero);
    }

    [Fact]
    public async Task Login_ToOneEmail_IsLimitedWhateverTheIp()
    {
        var client = _factory.CreateClient();

        for (var i = 0; i < PerEmailLimit; i++)
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await LoginAsync(client, "target@example.test", $"guess{i}")).StatusCode);

        var limited = await LoginAsync(client, "target@example.test", "one-more-guess");
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.RetryAfter?.Delta > TimeSpan.Zero);

        // Normalised like the login itself, so case and whitespace don't reset it.
        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await LoginAsync(client, "  TARGET@example.test ", "and-another")).StatusCode);

        // Another account is unaffected: the per-IP limit has not been reached.
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await LoginAsync(client, "someone-else@example.test", "wrong")).StatusCode);
    }

    /// <summary>
    /// The point of the limit: once spent, even the right password is refused, so an
    /// attacker cannot keep guessing and simply wait for a success.
    /// </summary>
    [Fact]
    public async Task Login_OnceLimited_RefusesEvenTheCorrectPassword()
    {
        var client = _factory.CreateClient();

        var register = await client.PostAsJsonAsync("/auth/register",
            new { email = "victim@example.test", displayName = "victim", password = "Corr3ct-horse" });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        for (var i = 0; i < PerEmailLimit; i++)
            await LoginAsync(client, "victim@example.test", $"guess{i}");

        var correct = await LoginAsync(client, "victim@example.test", "Corr3ct-horse");

        Assert.Equal(HttpStatusCode.TooManyRequests, correct.StatusCode);
    }

    [Fact]
    public async Task NonAuthEndpoints_AreNotLimited()
    {
        var userId = Guid.NewGuid();
        await _factory.WithDbAsync(async db =>
        {
            db.Users.Add(TestWorld.NewUser(userId, "busy@example.test"));
            await db.SaveChangesAsync();
        });

        var client = TestWorld.ClientFor(_factory, userId);

        for (var i = 0; i < PerIpLimit * 2; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/me/rounds")).StatusCode);
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/auth/login", new { email, password });
}
