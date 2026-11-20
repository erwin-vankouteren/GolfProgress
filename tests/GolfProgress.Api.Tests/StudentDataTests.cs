using GolfProgress.Domain.Entities;
using System.Net;
using System.Net.Http.Json;

namespace GolfProgress.Api.Tests;

/// <summary>
/// Issue #205: a student's rounds, practice and progress, read-only for their coach, and
/// the access check behind every route (#194).
/// </summary>
public sealed class StudentDataTests : IAsyncLifetime
{
    private readonly ApiFactory _factory = new();
    private readonly Guid _coachId = Guid.NewGuid();
    private readonly Guid _annId = Guid.NewGuid();
    private readonly Guid _benId = Guid.NewGuid();

    public async Task InitializeAsync() => await _factory.WithDbAsync(async db =>
    {
        db.Users.Add(TestWorld.NewUser(_coachId, "coach@example.test"));
        db.Users.Add(TestWorld.NewUser(_annId, "ann@example.test"));
        db.Users.Add(TestWorld.NewUser(_benId, "ben@example.test"));
        await db.SaveChangesAsync();
    });

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ACoach_ReadsTheStudentsRoundsAndProgress_WithTheGolfersContracts()
    {
        await LinkAsync();
        var (roundId, sessionId) = await SeedAnnAsync();
        var coach = Coach();

        var rounds = await coach.GetAsync($"/coach/students/{_annId}/rounds?take=1");
        Assert.Equal("2", rounds.Headers.GetValues("X-Total-Count").Single());
        var newest = Assert.Single((await rounds.Content.ReadFromJsonAsync<List<RoundPayload>>())!);
        Assert.Equal(roundId, newest.RoundId);
        Assert.Equal("Windy back nine", newest.Notes);

        var round = (await coach.GetFromJsonAsync<RoundPayload>($"/coach/students/{_annId}/rounds/{roundId}"))!;
        Assert.Equal(88, round.TotalStrokes);
        Assert.NotNull(round.FitnessScore);

        Assert.Equal(HttpStatusCode.OK, (await coach.GetAsync($"/coach/students/{_annId}/rounds/{roundId}/fitness-score")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await coach.GetAsync($"/coach/students/{_annId}/rounds/{roundId}/micro-wins")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await coach.GetAsync($"/coach/students/{_annId}/rounds/{roundId}/next-lever")).StatusCode);
        var reflections = await coach.GetFromJsonAsync<List<ReflectionPayload>>($"/coach/students/{_annId}/rounds/{roundId}/reflections");
        Assert.Equal("Tempo", Assert.Single(reflections!).WhatWorked);

        var timeline = (await coach.GetFromJsonAsync<TimelinePayload>($"/coach/students/{_annId}/timeline"))!;
        Assert.Equal(2, timeline.Count);
        Assert.Equal(HttpStatusCode.OK, (await coach.GetAsync($"/coach/students/{_annId}/timeline/monthly?months=3")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await coach.GetAsync($"/coach/students/{_annId}/monthly-reviews/2026-10-01")).StatusCode);

        var sessions = await coach.GetAsync($"/coach/students/{_annId}/practice-sessions");
        Assert.Equal("1", sessions.Headers.GetValues("X-Total-Count").Single());
        var session = (await coach.GetFromJsonAsync<SessionPayload>($"/coach/students/{_annId}/practice-sessions/{sessionId}"))!;
        Assert.Equal("Short putts", session.Notes);

        // None of it shows the golfer's email address.
        Assert.DoesNotContain("@example.test", await rounds.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task WithoutReflectionsShared_NotesAreLeftOut_AndReflectionsAre404()
    {
        await LinkAsync(shareReflections: false);
        var (roundId, sessionId) = await SeedAnnAsync();
        var coach = Coach();

        var list = await coach.GetFromJsonAsync<List<RoundPayload>>($"/coach/students/{_annId}/rounds");
        Assert.All(list!, r => Assert.Null(r.Notes));
        Assert.Null((await coach.GetFromJsonAsync<RoundPayload>($"/coach/students/{_annId}/rounds/{roundId}"))!.Notes);
        Assert.Null((await coach.GetFromJsonAsync<SessionPayload>($"/coach/students/{_annId}/practice-sessions/{sessionId}"))!.Notes);
        var sessions = await coach.GetAsync($"/coach/students/{_annId}/practice-sessions");
        Assert.DoesNotContain("Short putts", await sessions.Content.ReadAsStringAsync());

        var reflections = await coach.GetAsync($"/coach/students/{_annId}/rounds/{roundId}/reflections");
        Assert.Equal(HttpStatusCode.NotFound, reflections.StatusCode);
        Assert.DoesNotContain("Tempo", await reflections.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task WithoutPracticeShared_PracticeRoutesAre404()
    {
        await LinkAsync(sharePractice: false);
        var (_, sessionId) = await SeedAnnAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await Coach().GetAsync($"/coach/students/{_annId}/practice-sessions")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await Coach().GetAsync($"/coach/students/{_annId}/practice-sessions/{sessionId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Coach().GetAsync($"/coach/students/{_annId}/rounds")).StatusCode);
    }

    [Fact]
    public async Task WithoutAnActiveLink_EveryRouteIs404_BeforeValidation()
    {
        await LinkAsync(endedAt: DateTimeOffset.UtcNow.AddMinutes(-1));
        var (roundId, sessionId) = await SeedAnnAsync();
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

        foreach (var url in RoutesFor(_annId, roundId, sessionId).Append($"/coach/students/{_annId}/timeline?from=nope"))
        {
            var response = await Coach().GetAsync(url);
            Assert.True(response.StatusCode == HttpStatusCode.NotFound, $"{url} answered {response.StatusCode}");
            Assert.DoesNotContain("Windy", await response.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task AnotherGolfersRound_IsntReachableThroughAStudent()
    {
        await LinkAsync();
        var bensRound = await RoundAsync(_benId, "2026-10-03", 95, "Ben's notes");
        var bensSession = await PracticeAsync(_benId);

        foreach (var url in RoutesFor(_annId, bensRound, bensSession).Where(u => u.Contains(bensRound.ToString()) || u.Contains(bensSession.ToString())))
            Assert.Equal(HttpStatusCode.NotFound, (await Coach().GetAsync(url)).StatusCode);

        // Nor through the golfer's own routes, which still only answer the golfer.
        var annsRound = await RoundAsync(_annId, "2026-10-04", 90, null);
        Assert.Equal(HttpStatusCode.NotFound, (await Coach().GetAsync($"/rounds/{annsRound}")).StatusCode);
    }

    [Fact]
    public async Task RemovingTheCoach_EndsAccessOnTheNextRequest()
    {
        var linkId = await LinkAsync();
        var (roundId, _) = await SeedAnnAsync();
        Assert.Equal(HttpStatusCode.OK, (await Coach().GetAsync($"/coach/students/{_annId}/rounds/{roundId}")).StatusCode);

        await Golfer().DeleteAsync($"/me/coaches/{linkId}");

        Assert.Equal(HttpStatusCode.NotFound, (await Coach().GetAsync($"/coach/students/{_annId}/rounds/{roundId}")).StatusCode);
    }

    [Fact]
    public async Task BadDates_AreValidationErrors_ForACoach()
    {
        await LinkAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await Coach().GetAsync($"/coach/students/{_annId}/timeline?from=nope")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Coach().GetAsync($"/coach/students/{_annId}/monthly-reviews/october")).StatusCode);
    }

    [Fact]
    public async Task EveryRoute_NeedsASignedInUser()
    {
        var anonymous = _factory.CreateClient();
        foreach (var url in RoutesFor(_annId, Guid.NewGuid(), Guid.NewGuid()))
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(url)).StatusCode);
    }

    private static IEnumerable<string> RoutesFor(Guid golferId, Guid roundId, Guid sessionId)
    {
        var root = $"/coach/students/{golferId}";
        return new[]
        {
            $"{root}/rounds",
            $"{root}/rounds/{roundId}",
            $"{root}/rounds/{roundId}/fitness-score",
            $"{root}/rounds/{roundId}/micro-wins",
            $"{root}/rounds/{roundId}/next-lever",
            $"{root}/rounds/{roundId}/reflections",
            $"{root}/practice-sessions",
            $"{root}/practice-sessions/{sessionId}",
            $"{root}/timeline",
            $"{root}/timeline/monthly",
            $"{root}/monthly-reviews/2026-10-01",
        };
    }

    private HttpClient Coach() => TestWorld.ClientFor(_factory, _coachId);
    private HttpClient Golfer() => TestWorld.ClientFor(_factory, _annId);

    /// <summary>Two rounds (the newest with notes and a reflection), a session and a review.</summary>
    private async Task<(Guid RoundId, Guid SessionId)> SeedAnnAsync()
    {
        await RoundAsync(_annId, "2026-10-01", 92, null);
        var roundId = await RoundAsync(_annId, "2026-10-15", 88, "Windy back nine");
        (await Golfer().PostAsJsonAsync($"/rounds/{roundId}/reflections", new { focusRating = 4, whatWorked = "Tempo" }))
            .EnsureSuccessStatusCode();
        var sessionId = await PracticeAsync(_annId);
        (await Golfer().PostAsJsonAsync("/me/monthly-reviews/generate", new { monthStart = "2026-10-01" }))
            .EnsureSuccessStatusCode();
        return (roundId, sessionId);
    }

    private async Task<Guid> RoundAsync(Guid userId, string playedAt, int strokes, string? notes)
    {
        var response = await TestWorld.ClientFor(_factory, userId).PostAsJsonAsync("/me/rounds",
            new { playedAt, holes = 18, totalStrokes = strokes, totalPutts = 33, penalties = 2, threePutts = 2, notes });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RoundPayload>())!.RoundId;
    }

    private async Task<Guid> PracticeAsync(Guid userId)
    {
        var response = await TestWorld.ClientFor(_factory, userId).PostAsJsonAsync("/me/practice-sessions",
            new { practicedAt = "2026-10-10", sessionType = "Putting", durationMinutes = 30, notes = "Short putts" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SessionPayload>())!.PracticeSessionId;
    }

    private async Task<Guid> LinkAsync(bool shareReflections = true, bool sharePractice = true, DateTimeOffset? endedAt = null)
    {
        var linkId = Guid.NewGuid();
        await _factory.WithDbAsync(async db =>
        {
            db.CoachLinks.Add(new CoachLink
            {
                CoachLinkId = linkId,
                GolferId = _annId,
                CoachId = _coachId,
                ShareReflections = shareReflections,
                SharePractice = sharePractice,
                CreatedAt = DateTimeOffset.UtcNow,
                EndedAt = endedAt,
            });
            await db.SaveChangesAsync();
        });
        return linkId;
    }

    private sealed record RoundPayload(Guid RoundId, int TotalStrokes, string? Notes, int? FitnessScore);

    private sealed record SessionPayload(Guid PracticeSessionId, string? Notes);

    private sealed record ReflectionPayload(string? WhatWorked);

    private sealed record TimelinePayload(int Count);
}
