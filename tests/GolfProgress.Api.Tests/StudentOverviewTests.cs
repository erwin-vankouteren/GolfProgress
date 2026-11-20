using GolfProgress.Domain.Entities;
using System.Net;
using System.Net.Http.Json;

namespace GolfProgress.Api.Tests;

/// <summary>Issue #202: a coach's overview of a student, and the access check behind it (#194).</summary>
public sealed class StudentOverviewTests : IAsyncLifetime
{
    private readonly ApiFactory _factory = new();
    private readonly Guid _coachId = Guid.NewGuid();
    private readonly Guid _otherCoachId = Guid.NewGuid();
    private readonly Guid _annId = Guid.NewGuid();

    public async Task InitializeAsync() => await _factory.WithDbAsync(async db =>
    {
        db.Users.Add(TestWorld.NewUser(_coachId, "coach@example.test"));
        db.Users.Add(TestWorld.NewUser(_otherCoachId, "other@example.test"));
        var ann = TestWorld.NewUser(_annId, "ann@example.test");
        ann.DisplayName = "Ann";
        db.Users.Add(ann);
        await db.SaveChangesAsync();
    });

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ACoach_SeesTheTrend_NextFocus_Baseline_AndReflections_WithoutTheEmail()
    {
        await LinkAsync();
        var first = await RoundAsync("2026-10-01", 92);
        var last = await RoundAsync("2026-10-15", 88);
        await ReflectAsync(first, "Tempo on the tee");
        await ReflectAsync(last, "Lag putting");

        var response = await Coach().GetAsync($"/coach/students/{_annId}/overview");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("@example.test", await response.Content.ReadAsStringAsync());
        var overview = (await response.Content.ReadFromJsonAsync<OverviewPayload>())!;
        Assert.Equal("Ann", overview.DisplayName);
        Assert.Equal(last, overview.LastRound!.RoundId);
        Assert.Equal(new[] { first, last }, overview.RecentRounds.Select(r => r.RoundId));
        Assert.NotNull(overview.NextFocus);
        Assert.Equal(2, overview.Baseline!.SampleSize);
        Assert.Equal(new[] { "Lag putting", "Tempo on the tee" }, overview.LatestReflections.Select(r => r.WhatWorked));
        Assert.Equal("2026-10-15", overview.LatestReflections[0].PlayedAt);
    }

    [Fact]
    public async Task BeforeTheFirstRound_TheOverviewIsEmpty()
    {
        await LinkAsync();

        var overview = (await Coach().GetFromJsonAsync<OverviewPayload>($"/coach/students/{_annId}/overview"))!;

        Assert.Null(overview.LastRound);
        Assert.Empty(overview.RecentRounds);
        Assert.Null(overview.NextFocus);
        Assert.Null(overview.Baseline);
        Assert.Empty(overview.LatestReflections);
    }

    [Fact]
    public async Task ReflectionsThatArentShared_ArentInTheOverview()
    {
        await LinkAsync(shareReflections: false);
        await ReflectAsync(await RoundAsync("2026-10-01", 90), "Private thought");

        var response = await Coach().GetAsync($"/coach/students/{_annId}/overview");

        Assert.DoesNotContain("Private thought", await response.Content.ReadAsStringAsync());
        var overview = (await response.Content.ReadFromJsonAsync<OverviewPayload>())!;
        Assert.False(overview.ShareReflections);
        Assert.Empty(overview.LatestReflections);
    }

    [Fact]
    public async Task SwitchingReflectionsOff_AppliesToTheNextRequest()
    {
        var linkId = await LinkAsync();
        await ReflectAsync(await RoundAsync("2026-10-01", 90), "Tempo");
        Assert.Single((await OverviewAsync())!.LatestReflections);

        await Golfer().PatchAsJsonAsync($"/me/coaches/{linkId}", new { shareReflections = false });

        Assert.Empty((await OverviewAsync())!.LatestReflections);
    }

    [Fact]
    public async Task ARemovedCoach_LosesTheOverviewAtOnce()
    {
        var linkId = await LinkAsync();
        Assert.Equal(HttpStatusCode.OK, (await Coach().GetAsync($"/coach/students/{_annId}/overview")).StatusCode);

        await Golfer().DeleteAsync($"/me/coaches/{linkId}");

        Assert.Equal(HttpStatusCode.NotFound, (await Coach().GetAsync($"/coach/students/{_annId}/overview")).StatusCode);
    }

    [Fact]
    public async Task WithoutAnActiveLink_ItIs404_AndRevealsNothing()
    {
        await LinkAsync(coachId: _otherCoachId);
        await LinkAsync(endedAt: DateTimeOffset.UtcNow.AddMinutes(-1));
        await RoundAsync("2026-10-01", 90);
        await _factory.WithDbAsync(async db =>
        {
            // A pending invitation isn't access either.
            db.CoachInvitations.Add(new CoachInvitation
            {
                CoachInvitationId = Guid.NewGuid(),
                GolferId = _annId,
                Email = "coach@example.test",
                TokenHash = "hash",
                ShareReflections = true,
                SharePractice = true,
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            });
            await db.SaveChangesAsync();
        });

        var cases = new[]
        {
            (Coach(), $"/coach/students/{_annId}/overview"),
            (Coach(), $"/coach/students/{Guid.NewGuid()}/overview"),
            // The golfer can't reach their own data through the coach's route.
            (Golfer(), $"/coach/students/{_annId}/overview"),
        };
        foreach (var (client, url) in cases)
        {
            var response = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.DoesNotContain("Ann", await response.Content.ReadAsStringAsync());
        }

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _factory.CreateClient().GetAsync($"/coach/students/{_annId}/overview")).StatusCode);
    }

    private HttpClient Coach() => TestWorld.ClientFor(_factory, _coachId);
    private HttpClient Golfer() => TestWorld.ClientFor(_factory, _annId);

    private Task<OverviewPayload?> OverviewAsync() =>
        Coach().GetFromJsonAsync<OverviewPayload>($"/coach/students/{_annId}/overview");

    private async Task<Guid> LinkAsync(
        Guid? coachId = null, bool shareReflections = true, bool sharePractice = true, DateTimeOffset? endedAt = null)
    {
        var linkId = Guid.NewGuid();
        await _factory.WithDbAsync(async db =>
        {
            db.CoachLinks.Add(new CoachLink
            {
                CoachLinkId = linkId,
                GolferId = _annId,
                CoachId = coachId ?? _coachId,
                ShareReflections = shareReflections,
                SharePractice = sharePractice,
                CreatedAt = DateTimeOffset.UtcNow,
                EndedAt = endedAt,
            });
            await db.SaveChangesAsync();
        });
        return linkId;
    }

    private async Task<Guid> RoundAsync(string playedAt, int strokes)
    {
        var response = await Golfer().PostAsJsonAsync("/me/rounds",
            new { playedAt, holes = 18, totalStrokes = strokes, totalPutts = 33, penalties = 2, threePutts = 2 });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RoundPayload>())!.RoundId;
    }

    private async Task ReflectAsync(Guid roundId, string whatWorked) =>
        (await Golfer().PostAsJsonAsync($"/rounds/{roundId}/reflections", new { focusRating = 4, whatWorked }))
            .EnsureSuccessStatusCode();

    private sealed record RoundPayload(Guid RoundId);

    private sealed record OverviewPayload(
        string DisplayName,
        bool ShareReflections,
        RoundPayload? LastRound,
        List<RoundPayload> RecentRounds,
        object? NextFocus,
        BaselinePayload? Baseline,
        List<ReflectionPayload> LatestReflections);

    private sealed record BaselinePayload(int SampleSize);

    private sealed record ReflectionPayload(string? WhatWorked, string? PlayedAt);
}
