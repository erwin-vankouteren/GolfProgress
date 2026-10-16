using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Json;

namespace GolfProgress.Api.Tests;

/// <summary>
/// Regression guard for issue #62: errors the spec describes as ProblemDetails really
/// are. Several checks used to answer with a bare string (BadRequest("Email is
/// required."), Conflict(...), NotFound($"...")), which a client generated from the
/// spec could not have parsed. Validation errors are now keyed by field, like
/// [ApiController]'s own.
/// </summary>
public sealed class ProblemResponseTests : IAsyncLifetime
{
    private ApiFactory _factory = null!;
    private Owner _owner = null!;

    public async Task InitializeAsync()
    {
        _factory = new ApiFactory();
        (_owner, _) = await TestWorld.SeedTwoUsersAsync(_factory);
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Theory]
    // endpoint, body, field the error is keyed by
    [InlineData("/auth/register", """{"displayName":"x","password":"Passw0rd!"}""", "Email")]
    [InlineData("/auth/register", """{"email":"  ","displayName":"x","password":"Passw0rd!"}""", "Email")]
    [InlineData("/auth/login", """{"email":"a@example.test"}""", "Password")]
    [InlineData("/auth/refresh", """{}""", "RefreshToken")]
    public async Task MissingAuthFields_AreValidationProblemsKeyedByField(string endpoint, string body, string field)
    {
        var response = await _factory.CreateClient().PostAsync(endpoint,
            new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        await AssertValidationProblem(response, field);
    }

    /// <summary>
    /// Registration bounds the display name like PATCH /me does, so a name set here can
    /// always be saved again from Profile.
    /// </summary>
    [Theory]
    [InlineData(101, HttpStatusCode.BadRequest)]
    [InlineData(100, HttpStatusCode.OK)]
    public async Task RegistrationDisplayName_IsBounded(int length, HttpStatusCode expected)
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/auth/register",
            new { email = $"long{length}@example.test", displayName = new string('x', length), password = "Passw0rd!" });

        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.BadRequest) await AssertValidationProblem(response, "DisplayName");
    }

    [Fact]
    public async Task RegistrationDisplayName_CantBeBlank()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/auth/register",
            new { email = "blank@example.test", displayName = "   ", password = "Passw0rd!" });

        await AssertValidationProblem(response, "DisplayName");
    }

    [Fact]
    public async Task DuplicateRegistration_IsA409Problem()
    {
        var client = _factory.CreateClient();
        var body = new { email = "dupe@example.test", displayName = "dupe", password = "Passw0rd!" };

        await client.PostAsJsonAsync("/auth/register", body);
        var second = await client.PostAsJsonAsync("/auth/register", body);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("application/problem+json", second.Content.Headers.ContentType?.MediaType);
        var problem = await second.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Email already registered.", problem!.Title);
    }

    [Fact]
    public async Task InvalidRoundDate_IsKeyedByPlayedAt()
    {
        var response = await Client().PostAsJsonAsync("/me/rounds",
            new { playedAt = "not-a-date", holes = 18, totalStrokes = 90 });

        await AssertValidationProblem(response, "PlayedAt");
    }

    [Theory]
    [InlineData("/me/timeline?from=yesterday", "from")]
    [InlineData("/me/timeline?to=31-12-2026", "to")]   // day-first: accepted on a Dutch server before #62
    [InlineData("/me/timeline?to=12/31/2026", "to")]   // month-first: accepted on a US server before #62
    [InlineData("/me/timeline?from=2026-5-4", "from")] // not zero-padded
    [InlineData("/me/monthly-reviews/april", "monthStart")]
    public async Task InvalidDatesInTheUrl_AreKeyedByParameter(string url, string field)
    {
        await AssertValidationProblem(await Client().GetAsync(url), field);
    }

    [Fact]
    public async Task InvalidMonthStart_IsKeyedByField()
    {
        var response = await Client().PostAsJsonAsync("/me/monthly-reviews/generate", new { monthStart = "April" });

        await AssertValidationProblem(response, "MonthStart");
    }

    [Fact]
    public async Task ReflectionOnAMissingRound_IsA404ProblemWithDetail()
    {
        var missing = Guid.NewGuid();

        var response = await Client().PostAsJsonAsync($"/rounds/{missing}/reflections", new { focusRating = 3 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal($"Round {missing} not found.", problem!.Detail);
    }

    private HttpClient Client() => TestWorld.ClientFor(_factory, _owner.UserId);

    private static async Task AssertValidationProblem(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains(problem!.Errors.Keys, k => string.Equals(k, field, StringComparison.OrdinalIgnoreCase));
    }
}
