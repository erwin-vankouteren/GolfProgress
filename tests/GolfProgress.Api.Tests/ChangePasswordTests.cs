using GolfProgress.Api.Security;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace GolfProgress.Api.Tests;

/// <summary>
/// Issue #83: POST /me/password changes the password, keeps the current device signed
/// in and signs every other device out at its next refresh. The password policy it
/// enforces is shared with registration.
/// </summary>
public sealed class ChangePasswordTests : IAsyncLifetime
{
    private const string Email = "changer@example.test";
    private const string OldPassword = "Old-password-1";
    private const string NewPassword = "New-password-2";

    private ApiFactory _factory = null!;
    private Session _phone = null!;
    private Session _laptop = null!;

    public async Task InitializeAsync()
    {
        _factory = new ApiFactory();

        var registered = await _factory.CreateClient().PostAsJsonAsync("/auth/register",
            new { email = Email, displayName = "Changer", password = OldPassword });
        registered.EnsureSuccessStatusCode();
        _phone = await SessionFrom(registered);
        _laptop = await LoginAsync(OldPassword);
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Change_KeepsThisDevice_AndSignsOutTheOthers()
    {
        var response = await ChangeAsync(_phone, OldPassword, NewPassword);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // The phone, which changed it, refreshes as usual.
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(_phone)).StatusCode);
        // The laptop is signed out at its next refresh.
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(_laptop)).StatusCode);

        // The new password works and the old one doesn't.
        Assert.Equal(HttpStatusCode.OK, (await LoginResponseAsync(NewPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginResponseAsync(OldPassword)).StatusCode);

        _factory.WithDb(db => Assert.Single(db.Events.Where(e => e.EventType == "PasswordChanged")));
    }

    /// <summary>A refreshed session is still the same device: the new access token carries the new sid.</summary>
    [Fact]
    public async Task Change_AfterARefresh_StillKeepsThisDevice()
    {
        var refreshed = await SessionFrom(await RefreshAsync(_phone));

        Assert.Equal(HttpStatusCode.NoContent, (await ChangeAsync(refreshed, OldPassword, NewPassword)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(refreshed)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(_laptop)).StatusCode);
    }

    [Fact]
    public async Task WrongCurrentPassword_Is400_AndChangesNothing()
    {
        var response = await ChangeAsync(_phone, "not-my-password", NewPassword);

        // 400, not 401: a 401 would make the app sign the user out.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertFieldErrorAsync(response, "CurrentPassword");

        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(_laptop)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginResponseAsync(OldPassword)).StatusCode);
    }

    [Theory]
    [InlineData("short")]             // under 8 characters
    [InlineData("        ")]          // only spaces
    [InlineData(Email)]               // the email address
    [InlineData(OldPassword)]         // unchanged
    public async Task UnacceptableNewPassword_Is400_KeyedByNewPassword(string newPassword)
    {
        var response = await ChangeAsync(_phone, OldPassword, newPassword);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertFieldErrorAsync(response, "NewPassword");
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(_laptop)).StatusCode);
    }

    [Fact]
    public async Task Attempts_ShareThePerUserPasswordLimit()
    {
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.BadRequest, (await ChangeAsync(_phone, "guess-" + i, NewPassword)).StatusCode);

        var limited = await ChangeAsync(_phone, OldPassword, NewPassword);

        // Even the right password waits, so the limit reveals nothing.
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.RetryAfter?.Delta > TimeSpan.Zero);
    }

    /// <summary>A token from before sessions were recorded has no sid: every session ends.</summary>
    [Fact]
    public async Task Change_WithATokenWithoutASession_SignsOutEveryDevice()
    {
        var legacy = new Session(_phone.UserId, TestWorld.TokenFor(_phone.UserId), RefreshToken: "");

        Assert.Equal(HttpStatusCode.NoContent, (await ChangeAsync(legacy, OldPassword, NewPassword)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(_phone)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(_laptop)).StatusCode);
    }

    // ---------- The policy, shared with registration

    [Theory]
    [InlineData("1234567", false)]
    [InlineData("12345678", true)]
    [InlineData("correct horse battery staple", true)]   // spaces and passphrases are fine
    [InlineData("pässwörd", true)]
    [InlineData("🏌️🏌️🏌️🏌️", true)]                       // 8 code points, more UTF-16 units
    [InlineData("        ", false)]
    [InlineData("Changer@Example.test", false)]          // the email, in any case
    public void Policy_ChecksLengthAndTheEmail(string password, bool acceptable)
    {
        Assert.Equal(acceptable, PasswordPolicy.Check(password, Email) is null);
    }

    [Fact]
    public void Policy_BoundsTheLength()
    {
        Assert.Null(PasswordPolicy.Check(new string('x', PasswordPolicy.MaxLength), Email));
        Assert.NotNull(PasswordPolicy.Check(new string('x', PasswordPolicy.MaxLength + 1), Email));
    }

    [Fact]
    public async Task Register_AppliesThePolicy()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/auth/register",
            new { email = "weak@example.test", displayName = "Weak", password = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertFieldErrorAsync(response, "Password");
        _factory.WithDb(db => Assert.Empty(db.Users.Where(u => u.Email == "weak@example.test")));
    }

    // ---------- Helpers

    private Task<HttpResponseMessage> ChangeAsync(Session session, string currentPassword, string newPassword)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return client.PostAsJsonAsync("/me/password", new { currentPassword, newPassword });
    }

    private Task<HttpResponseMessage> RefreshAsync(Session session) =>
        _factory.CreateClient().PostAsJsonAsync("/auth/refresh", new { refreshToken = session.RefreshToken });

    private Task<HttpResponseMessage> LoginResponseAsync(string password) =>
        _factory.CreateClient().PostAsJsonAsync("/auth/login", new { email = Email, password });

    private async Task<Session> LoginAsync(string password)
    {
        var response = await LoginResponseAsync(password);
        response.EnsureSuccessStatusCode();
        return await SessionFrom(response);
    }

    private static async Task<Session> SessionFrom(HttpResponseMessage response)
    {
        var auth = (await response.Content.ReadFromJsonAsync<AuthPayload>())!;
        return new Session(auth.UserId, auth.Token, auth.RefreshToken!);
    }

    private static async Task AssertFieldErrorAsync(HttpResponseMessage response, string field)
    {
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains(problem!.Errors.Keys, k => string.Equals(k, field, StringComparison.OrdinalIgnoreCase));
    }

    private sealed record Session(Guid UserId, string AccessToken, string RefreshToken);

    private sealed record AuthPayload(Guid UserId, string Token, string? RefreshToken);
}
