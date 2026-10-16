using GolfProgress.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace GolfProgress.Api.Tests;

/// <summary>
/// Issue #84: DELETE /me deletes the account and everything in it, confirmed with the
/// password, and signs out every device. Required by the App Store and Google Play.
/// The same deletion against real SQL Server is in SqlServerPersistenceTests.
/// </summary>
public sealed class AccountDeletionTests : IAsyncLifetime
{
    private const string Password = "Correct-horse-1";

    private readonly FailingSave _failure = new();
    private ApiFactory _factory = null!;
    private Account _account = null!;
    private Owner _bob = null!;

    public async Task InitializeAsync()
    {
        _factory = new ApiFactory(_failure);
        (_, _bob) = await TestWorld.SeedTwoUsersAsync(_factory);
        _account = await RegisterWithDataAsync();
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Delete_RemovesEveryRowOfTheUser_AndNothingElse()
    {
        var response = await DeleteMeAsync(Password);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        _factory.WithDb(db =>
        {
            var id = _account.UserId;
            Assert.Empty(db.Users.Where(x => x.UserId == id));
            Assert.Empty(db.Rounds.Where(x => x.UserId == id));
            Assert.Empty(db.FitnessScores.Where(x => x.UserId == id));
            Assert.Empty(db.MicroWins.Where(x => x.UserId == id));
            Assert.Empty(db.Reflections.Where(x => x.UserId == id));
            Assert.Empty(db.PracticeSessions.Where(x => x.UserId == id));
            Assert.Empty(db.MonthlyReviews.Where(x => x.UserId == id));
            Assert.Empty(db.UserBaselines.Where(x => x.UserId == id));
            Assert.Empty(db.Events.Where(x => x.UserId == id));
            Assert.Empty(db.RefreshTokens.Where(x => x.UserId == id));

            // Someone else's account is untouched.
            Assert.Single(db.Users.Where(x => x.UserId == _bob.UserId));
            Assert.Single(db.Rounds.Where(x => x.UserId == _bob.UserId));
        });
    }

    [Fact]
    public async Task AfterDeletion_TheAccessTokenAndRefreshTokenStopWorking()
    {
        // Cache "this user exists" first, as an app in use would have.
        Assert.Equal(HttpStatusCode.OK, (await Client().GetAsync("/me")).StatusCode);

        await DeleteMeAsync(Password);

        // The access token is still within its 15 minutes, but is refused, reads and
        // writes alike: a write would otherwise hit a user that no longer exists.
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client().GetAsync("/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client().GetAsync("/me/rounds")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await Client().PostAsJsonAsync("/me/rounds", new { playedAt = "2026-05-02", totalStrokes = 90 })).StatusCode);

        // So the app refreshes, which fails too: the refresh token went with the account.
        var refresh = await _factory.CreateClient().PostAsJsonAsync("/auth/refresh", new { refreshToken = _account.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task WrongPassword_Is400_AndDeletesNothing()
    {
        var response = await DeleteMeAsync("not-the-password");

        // 400, not 401: a 401 would make the app sign the user out.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains(problem!.Errors.Keys, k => string.Equals(k, "Password", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(HttpStatusCode.OK, (await Client().GetAsync("/me")).StatusCode);
        _factory.WithDb(db => Assert.NotEmpty(db.Rounds.Where(x => x.UserId == _account.UserId)));
    }

    [Fact]
    public async Task MissingPassword_Is400()
    {
        var response = await Client().SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/me")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PasswordAttempts_AreLimitedPerUser()
    {
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.BadRequest, (await DeleteMeAsync("guess-" + i)).StatusCode);

        var limited = await DeleteMeAsync(Password);

        // Even the right password waits, so the limit reveals nothing.
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        Assert.Equal(HttpStatusCode.OK, (await Client().GetAsync("/me")).StatusCode);
    }

    [Fact]
    public async Task WhenTheSaveFails_NothingIsDeleted()
    {
        _failure.Enabled = true;

        var response = await DeleteMeAsync(Password);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        _factory.WithDb(db =>
        {
            Assert.Single(db.Users.Where(x => x.UserId == _account.UserId));
            Assert.NotEmpty(db.Rounds.Where(x => x.UserId == _account.UserId));
            Assert.NotEmpty(db.RefreshTokens.Where(x => x.UserId == _account.UserId));
        });
    }

    [Fact]
    public async Task Delete_RequiresAuthentication()
    {
        var response = await _factory.CreateClient().SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/me")
        {
            Content = JsonContent.Create(new { password = Password })
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Registers through the API, so the password is really hashed, then fills the
    /// account with one of everything: rounds (with scores, micro-wins, the baseline and
    /// events), a reflection, a practice session and a monthly review.
    /// </summary>
    private async Task<Account> RegisterWithDataAsync()
    {
        var registered = await _factory.CreateClient().PostAsJsonAsync("/auth/register",
            new { email = "leaving@example.test", displayName = "Leaving", password = Password });
        registered.EnsureSuccessStatusCode();
        var auth = (await registered.Content.ReadFromJsonAsync<AuthPayload>())!;
        var account = new Account(auth.UserId, auth.Token, auth.RefreshToken!);
        _account = account;

        var round = await Client().PostAsJsonAsync("/me/rounds",
            new { playedAt = "2026-05-01", totalStrokes = 90, totalPutts = 32, penalties = 2 });
        round.EnsureSuccessStatusCode();
        var roundId = (await round.Content.ReadFromJsonAsync<RoundPayload>())!.RoundId;
        (await Client().PostAsJsonAsync("/me/rounds", new { playedAt = "2026-05-08", totalStrokes = 88 })).EnsureSuccessStatusCode();

        (await Client().PostAsJsonAsync($"/rounds/{roundId}/reflections", new { focusRating = 4 })).EnsureSuccessStatusCode();
        (await Client().PostAsJsonAsync("/me/practice-sessions",
            new { practicedAt = "2026-05-03", sessionType = "Putting", durationMinutes = 30 })).EnsureSuccessStatusCode();
        (await Client().PostAsJsonAsync("/me/monthly-reviews/generate", new { monthStart = "2026-05-01" })).EnsureSuccessStatusCode();

        _factory.WithDb(db =>
        {
            // Each kind of row exists, so the deletion test can't pass vacuously.
            Assert.NotEmpty(db.FitnessScores.Where(x => x.UserId == account.UserId));
            Assert.NotEmpty(db.MicroWins.Where(x => x.UserId == account.UserId));
            Assert.NotEmpty(db.UserBaselines.Where(x => x.UserId == account.UserId));
            Assert.NotEmpty(db.MonthlyReviews.Where(x => x.UserId == account.UserId));
            Assert.NotEmpty(db.Events.Where(x => x.UserId == account.UserId));
            Assert.NotEmpty(db.RefreshTokens.Where(x => x.UserId == account.UserId));
        });

        return account;
    }

    private Task<HttpResponseMessage> DeleteMeAsync(string password) =>
        Client().SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/me")
        {
            Content = JsonContent.Create(new { password })
        });

    /// <summary>The account's own access token, from registration.</summary>
    private HttpClient Client()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _account.AccessToken);
        return client;
    }

    private sealed record Account(Guid UserId, string AccessToken, string RefreshToken);

    private sealed record AuthPayload(Guid UserId, string Token, string? RefreshToken);

    private sealed record RoundPayload(Guid RoundId);

    /// <summary>Stands in for a database failure while the account is being deleted.</summary>
    private sealed class FailingSave : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (Enabled && eventData.Context!.ChangeTracker.Entries<User>().Any(e => e.State == EntityState.Deleted))
                throw new InvalidOperationException("Simulated failure while deleting the account.");

            return base.SavingChangesAsync(eventData, result, ct);
        }
    }
}
