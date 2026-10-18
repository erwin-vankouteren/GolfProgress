using GolfProgress.Api.Email;
using GolfProgress.Api.Localization;
using GolfProgress.Domain.Entities;
using GolfProgress.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace GolfProgress.Api.Tests;

/// <summary>
/// The app language: the profile keeps the user's choice (null follows the device), and
/// the API's own texts come in the language a request asks for with Accept-Language,
/// while the database keeps English.
/// </summary>
public sealed class LanguageTests : IAsyncLifetime
{
    private ApiFactory _base = null!;
    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> _factory = null!;
    private readonly RecordingOutbox _outbox = new();
    private Owner _alice = null!;

    public async Task InitializeAsync()
    {
        _base = new ApiFactory();
        _factory = _base.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IEmailOutbox>();
            s.AddSingleton<IEmailOutbox>(_outbox);
        }));
        (_alice, _) = await TestWorld.SeedTwoUsersAsync(_base);
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        _base.Dispose();
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------ the profile setting

    [Fact]
    public async Task Profile_KeepsTheChosenLanguage_AndNullFollowsTheDevice()
    {
        Assert.Null((await Client().GetFromJsonAsync<UserPayload>("/me"))!.Language);

        var chosen = await PatchAsync("""{ "language": "dutch" }""");
        Assert.Equal(HttpStatusCode.OK, chosen.StatusCode);
        Assert.Equal("Dutch", (await chosen.Content.ReadFromJsonAsync<UserPayload>())!.Language);
        Assert.Equal("Dutch", (await Client().GetFromJsonAsync<UserPayload>("/me"))!.Language);

        // Other settings leave it alone.
        await PatchAsync("""{ "units": "Imperial" }""");
        Assert.Equal("Dutch", (await Client().GetFromJsonAsync<UserPayload>("/me"))!.Language);

        var cleared = await PatchAsync("""{ "language": null }""");
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        Assert.Null((await Client().GetFromJsonAsync<UserPayload>("/me"))!.Language);
    }

    [Theory]
    [InlineData("\"German\"")]
    [InlineData("\"nl\"")]
    [InlineData("1")]
    public async Task Profile_RejectsALanguageTheAppsDontComeIn(string json)
    {
        var response = await PatchAsync($$"""{ "language": {{json}} }""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null((await Client().GetFromJsonAsync<UserPayload>("/me"))!.Language);
    }

    // ------------------------------------------------------------ texts per request

    [Fact]
    public async Task MicroWins_AreShownInTheRequestsLanguage_AndStoredInEnglish()
    {
        var generated = await Client("nl-NL").PostAsync($"/rounds/{_alice.RoundId}/micro-wins/generate", null);
        generated.EnsureSuccessStatusCode();
        var dutch = (await generated.Content.ReadFromJsonAsync<GeneratedPayload>())!.Items;

        var stored = await StoredMicroWinsAsync();
        Assert.All(stored, mw => Assert.Equal(mw.Title, MicroWinTexts.For(mw, ApiTexts.English).Title));
        Assert.Equal(stored.Select(mw => MicroWinTexts.For(mw, ApiTexts.Dutch).Title), dutch.Select(w => w.Title));
        Assert.DoesNotContain(dutch, w => stored.Any(mw => mw.Title == w.Title || mw.Message == w.Message));

        var english = await Client("en-GB").GetFromJsonAsync<List<MicroWinPayload>>($"/rounds/{_alice.RoundId}/micro-wins");
        Assert.Equal(stored.Select(mw => mw.Title), english!.Select(w => w.Title));
        Assert.Equal(stored.Select(mw => mw.Message), english!.Select(w => w.Message));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("de-DE")]
    [InlineData("fr, de;q=0.8")]
    public async Task ALanguageTheAppsDontComeIn_GetsEnglish(string? acceptLanguage)
    {
        var lever = await Client(acceptLanguage).GetFromJsonAsync<LeverPayload>($"/rounds/{_alice.RoundId}/next-lever");

        Assert.Contains(lever!.Title, LeverTitles(ApiTexts.English));
    }

    [Fact]
    public async Task NextFocus_FollowsTheRequestsLanguage_WithItsNumberFormat()
    {
        // A baseline, so the rationale carries a decimal: 2 penalties against 1.5. The
        // round's three-putts and blow-ups are under theirs, so penalties are the lever.
        await _base.WithDbAsync(async db =>
        {
            db.UserBaselines.Add(new UserBaseline
            {
                UserId = _alice.UserId,
                AvgPenalties = 1.5m,
                AvgThreePutts = 1.5m,
                AvgBlowUpHoles = 1.5m,
                WindowRounds = 5,
                CompletenessRatio = 1m,
                ConfidenceLevel = ConfidenceLevel.Medium,
                ComputedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        });

        var dutch = await Client("nl").GetFromJsonAsync<LeverPayload>($"/rounds/{_alice.RoundId}/next-lever");
        var english = await Client("en").GetFromJsonAsync<LeverPayload>($"/rounds/{_alice.RoundId}/next-lever");

        Assert.Equal(english!.LeverKey, dutch!.LeverKey);
        Assert.Contains(dutch.Title, LeverTitles(ApiTexts.Dutch));
        Assert.Contains(english.Title, LeverTitles(ApiTexts.English));
        Assert.Equal("Strafslagen t.o.v. basislijn Δ=0,5", dutch.Rationale);
        Assert.Equal("Penalties vs baseline Δ=0.5", english.Rationale);
    }

    [Fact]
    public async Task Dashboard_IsCachedPerLanguage()
    {
        (await Client().PostAsync($"/rounds/{_alice.RoundId}/micro-wins/generate", null)).EnsureSuccessStatusCode();

        var english = await DashboardAsync("en");
        var dutch = await DashboardAsync("nl");
        var englishAgain = await DashboardAsync("en");

        Assert.Contains(english.NextLever!.Title, LeverTitles(ApiTexts.English));
        Assert.Contains(dutch.NextLever!.Title, LeverTitles(ApiTexts.Dutch));
        Assert.Equal(english.NextLever.Title, englishAgain.NextLever!.Title);
        Assert.NotEqual(english.MicroWins.Select(w => w.Title), dutch.MicroWins.Select(w => w.Title));
    }

    // ------------------------------------------------------------ email

    [Fact]
    public async Task ResetEmail_IsInTheChosenLanguage_WhateverTheRequestSays()
    {
        await RegisterAsync("chose-dutch@example.test");
        await _base.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Email == "chose-dutch@example.test");
            user.Language = AppLanguage.Dutch;
            await db.SaveChangesAsync();
        });

        await AskResetAsync("chose-dutch@example.test", acceptLanguage: "en-US");

        var email = Assert.Single(_outbox.Sent);
        Assert.Equal("Je GolfProgress-wachtwoord opnieuw instellen", email.Subject);
        Assert.Contains("Wachtwoord opnieuw instellen", email.Html);
    }

    [Theory]
    [InlineData("nl-BE", "Je GolfProgress-wachtwoord opnieuw instellen")]
    [InlineData("en-GB", "Reset your GolfProgress password")]
    [InlineData(null, "Reset your GolfProgress password")]
    public async Task ResetEmail_WithoutAChoice_IsInTheRequestsLanguage(string? acceptLanguage, string subject)
    {
        await RegisterAsync("device@example.test");

        await AskResetAsync("device@example.test", acceptLanguage);

        Assert.Equal(subject, Assert.Single(_outbox.Sent).Subject);
    }

    // ------------------------------------------------------------ the catalogue

    [Fact]
    public void EveryText_ExistsInEveryLanguage()
    {
        foreach (var texts in ApiTexts.All)
        {
            foreach (var text in Enum.GetValues<MicroWinText>())
            {
                var (title, message) = texts.MicroWin(text, "x");
                Assert.False(string.IsNullOrWhiteSpace(title), $"{texts.Language} {text} title");
                Assert.False(string.IsNullOrWhiteSpace(message), $"{texts.Language} {text} message");
            }
            foreach (var lever in Enum.GetValues<LeverText>())
            {
                Assert.False(string.IsNullOrWhiteSpace(texts.Lever(lever).Title));
                Assert.False(string.IsNullOrWhiteSpace(texts.LeverRationale(lever, null)));
                Assert.False(string.IsNullOrWhiteSpace(texts.LeverRationale(lever, -1.25m)));
            }
            foreach (var area in Enum.GetValues<GoalFocusArea>())
                Assert.False(string.IsNullOrWhiteSpace(texts.FocusLabel(area)));
            foreach (var category in Enum.GetValues<MicroWinCategory>())
                Assert.False(string.IsNullOrWhiteSpace(texts.TopMicroWinTheme(category)));
        }
    }

    [Fact]
    public void EnglishMicroWinMessages_AreDistinct_SoAStoredOneIsRecognised()
    {
        var messages = Enum.GetValues<MicroWinText>().Select(t => ApiTexts.English.MicroWin(t, "x").Message).ToList();

        Assert.Equal(messages.Count, messages.Distinct().Count());
    }

    [Fact]
    public void AStoredMicroWin_KeepsItsLowConfidenceNote_InAnotherLanguage()
    {
        var (title, message) = MicroWinTexts.Stored(MicroWinText.FocusSet, ConfidenceLevel.Low, "tee shots");
        var stored = new MicroWin
        {
            Title = title,
            Message = message,
            EvidenceJson = JsonDocument.Parse("""{ "focus": "TeeShots", "metric": "fairways_hit" }"""),
        };

        var dutch = MicroWinTexts.For(stored, ApiTexts.Dutch);

        Assert.Equal("Focus-area check: tee shots", title);
        Assert.Equal("Focuscheck: afslagen", dutch.Title);
        Assert.EndsWith(ApiTexts.Dutch.LowConfidenceSuffix, dutch.Message);
        Assert.DoesNotContain(ApiTexts.English.LowConfidenceSuffix, dutch.Message);
    }

    [Fact]
    public void AStoredMicroWinItDoesntRecognise_IsShownAsStored()
    {
        var stored = new MicroWin { Title = "Something new", Message = "Written by a later version." };

        Assert.Equal(("Something new", "Written by a later version."), MicroWinTexts.For(stored, ApiTexts.Dutch));
    }

    // ------------------------------------------------------------ helpers

    private HttpClient Client(string? acceptLanguage = null)
    {
        var client = TestWorld.ClientFor(_base, _alice.UserId);
        if (acceptLanguage is not null) client.DefaultRequestHeaders.Add("Accept-Language", acceptLanguage);
        return client;
    }

    private Task<HttpResponseMessage> PatchAsync(string json) =>
        Client().PatchAsync("/me", new StringContent(json, Encoding.UTF8, "application/json"));

    private async Task<List<MicroWin>> StoredMicroWinsAsync()
    {
        var stored = new List<MicroWin>();
        await _base.WithDbAsync(async db => stored = await db.MicroWins.AsNoTracking()
            .Where(mw => mw.RoundId == _alice.RoundId).OrderBy(mw => mw.CreatedAt).ToListAsync());
        return stored;
    }

    private async Task<DashboardPayload> DashboardAsync(string acceptLanguage) =>
        (await Client(acceptLanguage).GetFromJsonAsync<DashboardPayload>("/me/dashboard"))!;

    private static IEnumerable<string> LeverTitles(ApiTexts texts) =>
        Enum.GetValues<LeverText>().Select(l => texts.Lever(l).Title);

    private async Task RegisterAsync(string email) =>
        (await _factory.CreateClient().PostAsJsonAsync("/auth/register",
            new { email, displayName = "Reader", password = "Some-password-1" })).EnsureSuccessStatusCode();

    private async Task AskResetAsync(string email, string? acceptLanguage)
    {
        var client = _factory.CreateClient();
        if (acceptLanguage is not null) client.DefaultRequestHeaders.Add("Accept-Language", acceptLanguage);
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/auth/password-reset", new { email })).StatusCode);
    }

    private sealed class RecordingOutbox : IEmailOutbox
    {
        private readonly ConcurrentQueue<EmailMessage> _sent = new();

        public IReadOnlyCollection<EmailMessage> Sent => _sent.ToArray();

        public void Enqueue(EmailMessage message) => _sent.Enqueue(message);
    }

    private sealed record UserPayload(Guid UserId, string? Language);

    private sealed record MicroWinPayload(string Title, string Message);

    private sealed record GeneratedPayload(List<MicroWinPayload> Items);

    private sealed record LeverPayload(string LeverKey, string Title, string Recommendation, string Rationale);

    private sealed record DashboardPayload(LeverPayload? NextLever, List<MicroWinPayload> MicroWins);
}
