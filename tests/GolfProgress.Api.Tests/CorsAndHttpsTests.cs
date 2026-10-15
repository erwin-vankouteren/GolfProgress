using GolfProgress.Api.Configuration;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Net;

namespace GolfProgress.Api.Tests;

/// <summary>
/// Regression guard for issue #16: CORS follows Cors:AllowedOrigins (closed by
/// default), and plain-HTTP requests are redirected to HTTPS with HSTS sent on HTTPS.
/// </summary>
public sealed class CorsAndHttpsTests : IDisposable
{
    private const string AllowedOrigin = "https://app.example.test";

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

    private WebApplicationFactory<Program> WithAllowedOrigin() =>
        With(("Cors:AllowedOrigins:0", AllowedOrigin));

    [Fact]
    public async Task Preflight_FromAnAllowedOrigin_IsAnswered()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/me/rounds");
        request.Headers.Add("Origin", AllowedOrigin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "authorization");

        var response = await WithAllowedOrigin().CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(AllowedOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    /// <summary>Preflights for /auth/* must not use up the per-IP login limit.</summary>
    [Fact]
    public async Task Preflight_ToAuth_DoesNotCountAgainstTheRateLimit()
    {
        var client = WithAllowedOrigin().CreateClient();

        for (var i = 0; i < 15; i++)
        {
            var request = new HttpRequestMessage(HttpMethod.Options, "/auth/login");
            request.Headers.Add("Origin", AllowedOrigin);
            request.Headers.Add("Access-Control-Request-Method", "POST");

            Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(request)).StatusCode);
        }
    }

    [Fact]
    public async Task ActualRequest_FromAnAllowedOrigin_ExposesThePagingHeader()
    {
        // An empty list still carries X-Total-Count, so no data is needed.
        var client = WithAllowedOrigin().CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestWorld.TokenFor(Guid.NewGuid()));
        client.DefaultRequestHeaders.Add("Origin", AllowedOrigin);

        var response = await client.GetAsync("/me/rounds");

        Assert.Equal(AllowedOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        var exposed = string.Join(",", response.Headers.GetValues("Access-Control-Expose-Headers"));
        Assert.Contains("X-Total-Count", exposed);
    }

    [Fact]
    public async Task OtherOrigins_GetNoCorsHeaders()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/me/rounds");
        request.Headers.Add("Origin", "https://evil.example.test");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await WithAllowedOrigin().CreateClient().SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task ByDefault_NoOriginIsAllowed()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/me/rounds");
        request.Headers.Add("Origin", AllowedOrigin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await _root.CreateClient().SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Theory]
    [InlineData("*")]
    [InlineData("https://app.example.test/")]
    [InlineData("https://app.example.test/path")]
    [InlineData("app.example.test")]
    [InlineData("ftp://app.example.test")]
    public void InvalidOrigin_FailsAtStartup(string origin)
    {
        var factory = With(("Cors:AllowedOrigins:0", origin));

        Assert.ThrowsAny<Exception>(() => factory.CreateClient());
    }

    [Theory]
    [InlineData("https://app.example.test", true)]
    [InlineData("https://app.example.test:8443", true)]
    [InlineData("http://localhost:3000", true)]
    [InlineData("*", false)]
    [InlineData("https://app.example.test/", false)]
    [InlineData("https://user@app.example.test", false)]
    public void IsValidOrigin(string origin, bool expected)
        => Assert.Equal(expected, ClientCorsOptions.IsValidOrigin(origin));

    [Fact]
    public async Task PlainHttp_IsRedirectedToHttps()
    {
        var client = With(("https_port", "443"))
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("http://api.example.test/me/rounds?take=5");

        Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
        Assert.Equal("https://api.example.test/me/rounds?take=5", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Https_OutsideDevelopment_SendsHsts()
    {
        var client = _root.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://api.example.test")
        });

        var response = await client.GetAsync("/me/rounds");

        Assert.True(response.Headers.Contains("Strict-Transport-Security"));
    }
}
