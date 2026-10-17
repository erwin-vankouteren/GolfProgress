using GolfProgress.Api.Email;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace GolfProgress.Api.Tests;

/// <summary>
/// Issue #86: password reset by email. Asking reveals nothing about which accounts
/// exist; the link works once, for a limited time; using it signs out every session.
/// </summary>
public sealed class PasswordResetTests : IAsyncLifetime
{
    private const string Email = "forgetful@example.test";
    private const string OldPassword = "Old-password-1";
    private const string NewPassword = "New-password-2";

    private ApiFactory _base = null!;
    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> _factory = null!;
    private readonly RecordingOutbox _outbox = new();
    private string _refreshToken = "";

    public async Task InitializeAsync()
    {
        _base = new ApiFactory();
        _factory = _base.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IEmailOutbox>();
            s.AddSingleton<IEmailOutbox>(_outbox);
        }));

        var registered = await _factory.CreateClient().PostAsJsonAsync("/auth/register",
            new { email = Email, displayName = "Forgetful", password = OldPassword });
        registered.EnsureSuccessStatusCode();
        _refreshToken = (await registered.Content.ReadFromJsonAsync<AuthPayload>())!.RefreshToken!;
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        _base.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Asking_LooksTheSame_ForKnownAndUnknownAddresses()
    {
        var known = await AskAsync(" Forgetful@Example.test ");
        var unknown = await AskAsync("nobody@example.test");

        Assert.Equal(HttpStatusCode.Accepted, known.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, unknown.StatusCode);
        Assert.Equal(await known.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());

        // Only the account's owner gets an email, with the link.
        var email = Assert.Single(_outbox.Sent);
        Assert.Equal(Email, email.To);
        Assert.Contains("http://localhost:5173/reset-password?token=", email.PlainText);
    }

    [Fact]
    public async Task TheLink_SetsANewPassword_AndSignsOutEverySession()
    {
        await AskAsync(Email);

        var confirmed = await ConfirmAsync(TokenFrom(_outbox.Sent.Single()), NewPassword);

        Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(NewPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(OldPassword)).StatusCode);
        // The session from before the reset can no longer refresh.
        var refresh = await _factory.CreateClient().PostAsJsonAsync("/auth/refresh", new { refreshToken = _refreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task TheLink_WorksOnce_AndVoidsTheOthers()
    {
        await AskAsync(Email);
        await AskAsync(Email);
        var (first, second) = (TokenFrom(_outbox.Sent.First()), TokenFrom(_outbox.Sent.Last()));

        Assert.Equal(HttpStatusCode.NoContent, (await ConfirmAsync(first, NewPassword)).StatusCode);

        await AssertInvalidTokenAsync(await ConfirmAsync(first, "Another-password-3"));
        await AssertInvalidTokenAsync(await ConfirmAsync(second, "Another-password-3"));
    }

    [Fact]
    public async Task AnExpiredLink_IsRefused()
    {
        await AskAsync(Email);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GolfProgress.Infrastructure.Persistence.GolfProgressDbContext>();
            foreach (var token in db.PasswordResetTokens) token.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        await AssertInvalidTokenAsync(await ConfirmAsync(TokenFrom(_outbox.Sent.Single()), NewPassword));
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(OldPassword)).StatusCode);
    }

    [Fact]
    public async Task AWeakNewPassword_IsRefused_AndTheLinkStillWorks()
    {
        await AskAsync(Email);
        var token = TokenFrom(_outbox.Sent.Single());

        var weak = await ConfirmAsync(token, "short");
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        var problem = await weak.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains("NewPassword", problem!.Errors.Keys);

        Assert.Equal(HttpStatusCode.NoContent, (await ConfirmAsync(token, NewPassword)).StatusCode);
    }

    [Fact]
    public async Task AnAccount_GetsAFewEmailsAnHour_AtMost()
    {
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Accepted, (await AskAsync(Email)).StatusCode);

        Assert.Equal(3, _outbox.Sent.Count);
    }

    [Fact]
    public async Task OnlyTheTokensHash_IsStored()
    {
        await AskAsync(Email);
        var token = TokenFrom(_outbox.Sent.Single());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GolfProgress.Infrastructure.Persistence.GolfProgressDbContext>();
        Assert.DoesNotContain(db.PasswordResetTokens, t => t.TokenHash == token);
        Assert.Single(db.PasswordResetTokens);
    }

    private Task<HttpResponseMessage> AskAsync(string email) =>
        _factory.CreateClient().PostAsJsonAsync("/auth/password-reset", new { email });

    private Task<HttpResponseMessage> ConfirmAsync(string token, string newPassword) =>
        _factory.CreateClient().PostAsJsonAsync("/auth/password-reset/confirm", new { token, newPassword });

    private Task<HttpResponseMessage> LoginAsync(string password) =>
        _factory.CreateClient().PostAsJsonAsync("/auth/login", new { email = Email, password });

    private static async Task AssertInvalidTokenAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains("Token", problem!.Errors.Keys);
    }

    private static string TokenFrom(EmailMessage email) =>
        Uri.UnescapeDataString(Regex.Match(email.PlainText, @"token=([^\s]+)").Groups[1].Value);

    private sealed class RecordingOutbox : IEmailOutbox
    {
        private readonly ConcurrentQueue<EmailMessage> _sent = new();

        public IReadOnlyCollection<EmailMessage> Sent => _sent.ToArray();

        public void Enqueue(EmailMessage message) => _sent.Enqueue(message);
    }

    private sealed record AuthPayload(Guid UserId, string Token, string? RefreshToken);
}
