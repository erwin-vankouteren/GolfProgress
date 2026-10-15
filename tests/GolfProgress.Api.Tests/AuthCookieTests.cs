using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Net.Http.Headers;
using System.Net;
using System.Net.Http.Json;

namespace GolfProgress.Api.Tests;

/// <summary>
/// Regression guard for issue #68: a web client receives the refresh token as an
/// HttpOnly cookie instead of in the body, and refreshes and signs out with it. Mobile
/// clients, which don't ask for the cookie, are unchanged.
/// </summary>
public sealed class AuthCookieTests : IDisposable
{
    private const string WebOrigin = "https://app.example.test";

    private readonly ApiFactory _root = new();
    private readonly WebApplicationFactory<Program> _factory;

    public AuthCookieTests()
    {
        _factory = _root.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["Cors:AllowedOrigins:0"] = WebOrigin })));
    }

    public void Dispose()
    {
        _factory.Dispose();
        _root.Dispose();
    }

    /// <summary>
    /// A client that doesn't keep cookies itself, so each test controls exactly which
    /// cookie is sent -- as the browser would.
    /// </summary>
    private HttpClient Client() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false, BaseAddress = new Uri("https://localhost") });

    private static HttpRequestMessage Post(string path, object? body = null, bool cookieMode = false, string? cookie = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body ?? new { }) };
        if (cookieMode) request.Headers.Add("X-Refresh-Token-Mode", "cookie");
        if (cookie is not null) request.Headers.Add("Cookie", $"gp_refresh={cookie}");
        return request;
    }

    private static object Registration(string email = "web@example.test") =>
        new { email, displayName = "web", password = "Corr3ct-horse" };

    private static SetCookieHeaderValue RefreshCookie(HttpResponseMessage response)
    {
        var header = Assert.Single(response.Headers.GetValues("Set-Cookie"), v => v.StartsWith("gp_refresh="));
        return SetCookieHeaderValue.Parse(header);
    }

    [Fact]
    public async Task CookieMode_PutsTheRefreshTokenInASecureHttpOnlyCookie_NotTheBody()
    {
        var response = await Client().SendAsync(Post("/auth/register", Registration(), cookieMode: true));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthPayload>();
        Assert.Null(body!.RefreshToken);
        Assert.False(string.IsNullOrEmpty(body.Token));

        var cookie = RefreshCookie(response);
        Assert.True(cookie.HttpOnly);
        Assert.True(cookie.Secure);
        Assert.Equal(SameSiteMode.Strict, cookie.SameSite);
        Assert.Equal("/auth", cookie.Path.Value);
        Assert.True(cookie.Expires > DateTimeOffset.UtcNow.AddDays(13));
        Assert.False(string.IsNullOrEmpty(cookie.Value.Value));
    }

    [Fact]
    public async Task WithoutCookieMode_TheRefreshTokenStaysInTheBody_ForMobileClients()
    {
        var response = await Client().SendAsync(Post("/auth/register", Registration()));

        var body = await response.Content.ReadFromJsonAsync<AuthPayload>();
        Assert.False(string.IsNullOrEmpty(body!.RefreshToken));
        Assert.False(response.Headers.TryGetValues("Set-Cookie", out _));
    }

    [Fact]
    public async Task Refresh_WithOnlyTheCookie_RotatesItInTheCookie()
    {
        var client = Client();
        var signIn = await client.SendAsync(Post("/auth/register", Registration(), cookieMode: true));
        var first = RefreshCookie(signIn).Value.Value!;

        // The web client sends an empty body; the browser adds the cookie.
        var refreshed = await client.SendAsync(Post("/auth/refresh", cookie: first));

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Null((await refreshed.Content.ReadFromJsonAsync<AuthPayload>())!.RefreshToken);
        var second = RefreshCookie(refreshed).Value.Value!;
        Assert.NotEqual(first, second);

        // Rotation: the old cookie no longer works.
        var reused = await client.SendAsync(Post("/auth/refresh", cookie: first));
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithNoTokenAnywhere_IsAValidationProblem()
    {
        var response = await Client().SendAsync(Post("/auth/refresh"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesTheCookieTokenAndClearsTheCookie()
    {
        var client = Client();
        var signIn = await client.SendAsync(Post("/auth/register", Registration(), cookieMode: true));
        var token = RefreshCookie(signIn).Value.Value!;

        var logout = await client.SendAsync(Post("/auth/logout", cookie: token));

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        var cleared = RefreshCookie(logout);
        Assert.True(cleared.Expires < DateTimeOffset.UtcNow);
        Assert.Equal("/auth", cleared.Path.Value);

        var afterLogout = await client.SendAsync(Post("/auth/refresh", cookie: token));
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    [Fact]
    public async Task Logout_WithoutAnyToken_StillSucceeds()
    {
        var response = await Client().SendAsync(Post("/auth/logout"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    /// <summary>
    /// Credentials (the cookie) are allowed cross-origin on /auth/* only; the rest of the
    /// API is authorised by the bearer token and never needs them.
    /// </summary>
    [Theory]
    [InlineData("/auth/refresh", true)]
    [InlineData("/me/rounds", false)]
    public async Task CorsCredentials_AreAllowedOnAuthOnly(string path, bool allowed)
    {
        var preflight = new HttpRequestMessage(HttpMethod.Options, path);
        preflight.Headers.Add("Origin", WebOrigin);
        preflight.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await Client().SendAsync(preflight);

        Assert.Equal(WebOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal(allowed, response.Headers.TryGetValues("Access-Control-Allow-Credentials", out var values)
            && values.Single() == "true");
    }

    private sealed record AuthPayload(Guid UserId, string Token, string? RefreshToken);
}
