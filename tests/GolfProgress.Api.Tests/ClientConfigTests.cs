using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http.Json;

namespace GolfProgress.Api.Tests;

/// <summary>
/// Regression guard for issue #66: GET /client-config publishes the minimum supported
/// app version per platform, anonymously, from configuration validated at startup.
/// </summary>
public sealed class ClientConfigTests : IDisposable
{
    private readonly ApiFactory _root = new();
    private readonly List<IDisposable> _factories = new();

    public void Dispose()
    {
        foreach (var f in _factories) f.Dispose();
        _root.Dispose();
    }

    private WebApplicationFactory<Program> With(params (string key, string value)[] settings)
    {
        var factory = _root.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(settings.ToDictionary(s => s.key, s => (string?)s.value))));
        _factories.Add(factory);
        return factory;
    }

    [Fact]
    public async Task ReturnsTheDefaults_WithoutAToken()
    {
        var response = await _root.CreateClient().GetAsync("/client-config");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var config = await response.Content.ReadFromJsonAsync<ConfigPayload>();
        Assert.Equal(new VersionsPayload("1.0.0", "1.0.0", "1.0.0"), config!.MinimumVersions);
    }

    [Fact]
    public async Task ReturnsTheConfiguredVersions()
    {
        var client = With(
            ("ClientConfig:MinimumVersions:Ios", "1.4.0"),
            ("ClientConfig:MinimumVersions:Android", "1.3.2")).CreateClient();

        var config = await client.GetFromJsonAsync<ConfigPayload>("/client-config");

        Assert.Equal(new VersionsPayload("1.4.0", "1.3.2", "1.0.0"), config!.MinimumVersions);
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData("v1.0.0")]
    [InlineData("1.0.0-beta")]
    [InlineData("")]
    public void InvalidVersion_FailsAtStartup(string version)
    {
        var factory = With(("ClientConfig:MinimumVersions:Web", version));

        Assert.ThrowsAny<Exception>(() => factory.CreateClient());
    }

    [Fact]
    public async Task IsCacheableForFiveMinutes()
    {
        var response = await _root.CreateClient().GetAsync("/client-config");

        Assert.True(response.Headers.CacheControl!.Public);
        Assert.Equal(TimeSpan.FromSeconds(300), response.Headers.CacheControl.MaxAge);
    }

    /// <summary>
    /// Apps call this on every launch, so it has its own limit: using it up must not
    /// block signing in, and it isn't limited as tightly as /auth/*.
    /// </summary>
    [Fact]
    public async Task HasItsOwnRateLimit_SeparateFromSignIn()
    {
        var client = With(
            ("RateLimiting:Anonymous:PerIpPermitLimit", "3"),
            ("RateLimiting:Anonymous:PerIpWindowSeconds", "60")).CreateClient();

        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/client-config")).StatusCode);

        var limited = await client.GetAsync("/client-config");
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.RetryAfter?.Delta > TimeSpan.Zero);

        // The sign-in limit is untouched: a login attempt still gets through to the
        // credentials check (401), not a 429.
        var login = await client.PostAsJsonAsync("/auth/login", new { email = "a@example.test", password = "wrong" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    private sealed record VersionsPayload(string Ios, string Android, string Web);
    private sealed record ConfigPayload(VersionsPayload MinimumVersions);
}
