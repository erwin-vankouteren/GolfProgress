using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Net;
using System.Net.Http.Json;

namespace GolfProgress.Api.Tests;

/// <summary>
/// Regression guard for issue #7: exceptions that escape a controller become
/// ProblemDetails responses -- 404 for NotFoundException, a generic 500 for anything
/// else -- instead of raw 500s, and never leak exception details.
/// </summary>
public sealed class ExceptionHandlingTests : IDisposable
{
    private const string SecretDetail = "connection to db-prod-07 failed at Foo.Bar()";

    private readonly FailingSave _failure = new();
    private readonly ApiFactory _factory;

    public ExceptionHandlingTests() => _factory = new ApiFactory(_failure);

    public void Dispose() => _factory.Dispose();

    /// <summary>
    /// A valid token for a user who does not exist. The dashboard used to catch this
    /// locally; it is now the global handler's 404.
    /// </summary>
    [Fact]
    public async Task NotFoundException_IsA404ProblemDetails()
    {
        var client = TestWorld.ClientFor(_factory, Guid.NewGuid());

        var response = await client.GetAsync("/me/dashboard");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(404, problem!.Status);
        Assert.Equal("User not found.", problem.Detail);
        Assert.True(problem.Extensions.ContainsKey("traceId"));
    }

    [Fact]
    public async Task UnexpectedException_IsA500ProblemDetails_WithoutTheExceptionDetails()
    {
        var (alice, _) = await SeedWithoutFailingAsync();
        var client = TestWorld.ClientFor(_factory, alice.UserId);

        var response = await client.PostAsJsonAsync("/me/rounds",
            new { playedAt = "2026-06-01", holes = 18, totalStrokes = 88 });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(SecretDetail, body);
        Assert.DoesNotContain("FailingSave", body);   // no type names or stack trace

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("An unexpected error occurred.", problem!.Title);
        Assert.True(problem.Extensions.ContainsKey("traceId"));
    }

    private async Task<(Owner, Owner)> SeedWithoutFailingAsync()
    {
        _failure.Enabled = false;
        try { return await TestWorld.SeedTwoUsersAsync(_factory); }
        finally { _failure.Enabled = true; }
    }

    /// <summary>Stands in for an unexpected fault deep in a request.</summary>
    private sealed class FailingSave : SaveChangesInterceptor
    {
        public bool Enabled { get; set; } = true;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (Enabled) throw new InvalidOperationException(SecretDetail);
            return base.SavingChangesAsync(eventData, result, ct);
        }
    }
}
