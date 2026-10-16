using Microsoft.AspNetCore.Hosting;
using System.Net;
using System.Net.Http.Json;

namespace GolfProgress.Api.Tests;

/// <summary>
/// Regression guard for issues #6 and #85: signing in is throttled per client IP and
/// per email address, and keeping a session (refresh, logout) has its own, far higher,
/// per-IP limit, so many real users behind one carrier or Wi-Fi IP aren't throttled
/// together.
///
/// Uses the default limits: a burst of 30 sign-in requests per IP, refilling at 10 a
/// minute; 300 refresh or logout requests per IP per minute; and 5 login attempts per
/// email per five minutes. Each test gets its own factory, so its own limiter state.
/// Under TestServer every request shares one (unknown) client IP, unless the test
/// sends X-Forwarded-For with forwarded headers enabled.
/// </summary>
public sealed class AuthRateLimitTests : IDisposable
{
    private const int SignInBurst = 30;
    private const int SessionLimit = 300;
    private const int PerEmailLimit = 5;

    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task SignIn_FromOneIp_AllowsABurst_ThenIsLimited()
    {
        var client = _factory.CreateClient();

        // Spread over distinct emails, logins and registrations, so only the per-IP
        // limit applies: a group signing in together at a club.
        for (var i = 0; i < SignInBurst; i++)
        {
            var response = i % 2 == 0
                ? await LoginAsync(client, $"user{i}@example.test", "wrong")
                : await RegisterAsync(client, $"new{i}@example.test");

            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        var limited = await RegisterAsync(client, "late@example.test");

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.RetryAfter?.Delta > TimeSpan.Zero);
    }

    /// <summary>
    /// Devices behind a busy IP keep refreshing their sessions even once sign-in from
    /// that IP is limited: the two don't share an allowance.
    /// </summary>
    [Fact]
    public async Task Refresh_IsNotLimitedBySignInTraffic()
    {
        var client = _factory.CreateClient();

        for (var i = 0; i <= SignInBurst; i++)
            await LoginAsync(client, $"user{i}@example.test", "wrong");
        Assert.Equal(HttpStatusCode.TooManyRequests, (await LoginAsync(client, "another@example.test", "wrong")).StatusCode);

        var refresh = await client.PostAsJsonAsync("/auth/refresh", new { refreshToken = "not-a-token" });
        var logout = await client.PostAsJsonAsync("/auth/logout", new { refreshToken = "not-a-token" });

        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
    }

    /// <summary>
    /// Sized for many devices: every signed-in device refreshes every 15 minutes, so a
    /// carrier IP shared by 1,000 active users makes about 67 refreshes a minute.
    /// </summary>
    [Fact]
    public async Task Refresh_FromOneIp_HasItsOwnHigherLimit()
    {
        var client = _factory.CreateClient();

        for (var i = 0; i < SessionLimit; i++)
            Assert.NotEqual(HttpStatusCode.TooManyRequests,
                (await client.PostAsJsonAsync("/auth/refresh", new { refreshToken = "not-a-token" })).StatusCode);

        var limited = await client.PostAsJsonAsync("/auth/refresh", new { refreshToken = "not-a-token" });

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.RetryAfter?.Delta > TimeSpan.Zero);
    }

    /// <summary>
    /// Behind App Service (ASPNETCORE_FORWARDEDHEADERS_ENABLED=true) each client gets its
    /// own allowance, keyed by the address the proxy appends to X-Forwarded-For. An
    /// address the client puts there itself comes first and is ignored, so it can't be
    /// used to get a fresh allowance.
    /// </summary>
    [Fact]
    public async Task BehindAProxy_TheLimitIsPerClient_AndCantBeSpoofed()
    {
        using var behindProxy = _factory.WithWebHostBuilder(b => b.UseSetting("FORWARDEDHEADERS_ENABLED", "true"));
        var client = behindProxy.CreateClient();

        Task<HttpResponseMessage> LoginFrom(string forwardedFor, string email)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/auth/login")
            {
                Content = JsonContent.Create(new { email, password = "wrong" })
            };
            request.Headers.Add("X-Forwarded-For", forwardedFor);
            return client.SendAsync(request);
        }

        // Spend one client's allowance, each time claiming to be someone else.
        for (var i = 0; i < SignInBurst; i++)
            await LoginFrom($"203.0.113.{i}, 198.51.100.7", $"user{i}@example.test");

        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await LoginFrom("203.0.113.200, 198.51.100.7", "spoofer@example.test")).StatusCode);

        // A different client behind the proxy is unaffected.
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await LoginFrom("198.51.100.8", "neighbour@example.test")).StatusCode);
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

        for (var i = 0; i < SignInBurst + 10; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/me/rounds")).StatusCode);
    }

    private static Task<HttpResponseMessage> RegisterAsync(HttpClient client, string email) =>
        client.PostAsJsonAsync("/auth/register", new { email, displayName = "new", password = "Passw0rd!" });

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/auth/login", new { email, password });
}
