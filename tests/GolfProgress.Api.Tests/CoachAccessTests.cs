using GolfProgress.Api.Security;
using GolfProgress.Domain.Entities;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace GolfProgress.Api.Tests;

/// <summary>
/// Issue #194: one access rule for every coach and feedback request. The caller is the
/// golfer, or a coach with an active link whose sharing covers the item; anyone else gets
/// 404, so they can't tell whether a golfer or item exists. Each endpoint is probed in every
/// state that isn't access: no link, a pending invitation, an ended link, and sharing
/// switched off. <see cref="EveryCoachAndFeedbackRoute_IsProbed"/> fails when a route is
/// added without a probe here.
/// </summary>
public sealed class CoachAccessTests : IAsyncLifetime
{
    private const string RoundNotes = "Windy back nine";
    private const string WhatWorked = "Kept my tempo";
    private const string CoachNote = "Coach note";

    private readonly ApiFactory _factory = new();
    private readonly Guid _coachId = Guid.NewGuid();
    private readonly Guid _annId = Guid.NewGuid();
    private readonly Guid _strangerId = Guid.NewGuid();
    private readonly Guid _invitedId = Guid.NewGuid();
    private readonly Guid _linkId = Guid.NewGuid();

    private Guid _roundId;
    private Guid _reflectionId;
    private Guid _sessionId;
    private Guid _goalId;
    private readonly Dictionary<string, Guid> _coachMessages = new();

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public async Task InitializeAsync()
    {
        await _factory.WithDbAsync(async db =>
        {
            db.Users.AddRange(
                TestWorld.NewUser(_coachId, "coach@example.test"),
                TestWorld.NewUser(_annId, "ann@example.test"),
                TestWorld.NewUser(_strangerId, "stranger@example.test"),
                TestWorld.NewUser(_invitedId, "invited@example.test"));
            db.CoachLinks.Add(new CoachLink
            {
                CoachLinkId = _linkId,
                GolferId = _annId,
                CoachId = _coachId,
                ShareReflections = true,
                SharePractice = true,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            // Invited by Ann, but not accepted yet: that isn't access.
            db.CoachInvitations.Add(new CoachInvitation
            {
                CoachInvitationId = Guid.NewGuid(),
                GolferId = _annId,
                Email = "invited@example.test",
                TokenHash = "hash",
                ShareReflections = true,
                SharePractice = true,
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            });
            await db.SaveChangesAsync();
        });

        var golfer = Client(_annId);
        var round = await golfer.PostAsJsonAsync("/me/rounds",
            new { playedAt = "2026-10-15", holes = 18, totalStrokes = 88, totalPutts = 33, penalties = 2, threePutts = 2, notes = RoundNotes });
        round.EnsureSuccessStatusCode();
        _roundId = (await round.Content.ReadFromJsonAsync<IdPayload>())!.RoundId!.Value;

        var reflection = await golfer.PostAsJsonAsync($"/rounds/{_roundId}/reflections", new { focusRating = 4, whatWorked = WhatWorked });
        reflection.EnsureSuccessStatusCode();
        _reflectionId = (await reflection.Content.ReadFromJsonAsync<IdPayload>())!.ReflectionId!.Value;

        var session = await golfer.PostAsJsonAsync("/me/practice-sessions",
            new { practicedAt = "2026-10-10", sessionType = "Putting", durationMinutes = 30, notes = "Short putts" });
        session.EnsureSuccessStatusCode();
        _sessionId = (await session.Content.ReadFromJsonAsync<IdPayload>())!.PracticeSessionId!.Value;

        (await golfer.PostAsJsonAsync("/me/monthly-reviews/generate", new { monthStart = "2026-10-01" })).EnsureSuccessStatusCode();

        var coach = Client(_coachId);
        var goal = await coach.PostAsJsonAsync($"/coach/students/{_annId}/practice-goals", Goal("Lag putting"));
        goal.EnsureSuccessStatusCode();
        _goalId = (await goal.Content.ReadFromJsonAsync<IdPayload>())!.PracticeGoalId!.Value;

        foreach (var (type, targetId) in Targets())
        {
            var message = await coach.PostAsJsonAsync("/feedback",
                new { golferId = _annId, targetType = type, targetId, text = $"{CoachNote} on the {type}" });
            message.EnsureSuccessStatusCode();
            _coachMessages[type] = (await message.Content.ReadFromJsonAsync<IdPayload>())!.FeedbackMessageId!.Value;
        }
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// The control for the 404s below: with an active link sharing everything, the coach
    /// reaches every probe, so a 404 later comes from the access rule, not a bad request.
    /// </summary>
    [Fact]
    public async Task WithEverythingShared_TheCoachReachesEveryProbe()
    {
        // What changes something last, and stopping coaching at the very end.
        foreach (var probe in Probes().OrderBy(p => p.Changes).ThenBy(p => p.Template == StopCoaching))
        {
            var response = await Client(_coachId).SendAsync(probe.Request());
            Assert.True(response.IsSuccessStatusCode, $"{probe} answered {(int)response.StatusCode}");
        }
    }

    [Theory]
    [InlineData("no link")]
    [InlineData("a pending invitation")]
    [InlineData("a link the golfer ended")]
    [InlineData("a link the coach ended")]
    public async Task WithoutAnActiveLink_EveryProbeIs404_AndChangesNothing(string state)
    {
        var caller = state switch
        {
            "no link" => _strangerId,
            "a pending invitation" => _invitedId,
            _ => _coachId,
        };
        // Through the API, so this also shows that ending a link applies to the next request.
        if (state == "a link the golfer ended")
            Assert.Equal(HttpStatusCode.NoContent, (await Client(_annId).DeleteAsync($"/me/coaches/{_linkId}")).StatusCode);
        if (state == "a link the coach ended")
            Assert.Equal(HttpStatusCode.NoContent, (await Client(_coachId).DeleteAsync($"/coach/students/{_annId}")).StatusCode);

        foreach (var probe in Probes())
            await AssertNotFoundAsync(caller, probe);

        await AssertNothingChangedAsync();
    }

    [Theory]
    [InlineData(CoachShare.Reflections)]
    [InlineData(CoachShare.Practice)]
    public async Task SwitchingSharingOff_Is404_OnTheNextRequest_ForWhatItCovered(CoachShare share)
    {
        var change = share == CoachShare.Reflections ? (object)new { shareReflections = false } : new { sharePractice = false };
        Assert.Equal(HttpStatusCode.OK, (await Client(_annId).PatchAsJsonAsync($"/me/coaches/{_linkId}", change)).StatusCode);

        foreach (var probe in Probes().Where(p => p.Needs == share))
            await AssertNotFoundAsync(_coachId, probe);
        await AssertNothingChangedAsync(share);

        // The rest is still reachable.
        foreach (var probe in Probes().Where(p => p.Needs != share && !p.Changes))
        {
            var response = await Client(_coachId).SendAsync(probe.Request());
            Assert.True(response.IsSuccessStatusCode, $"{probe} answered {(int)response.StatusCode} with {share} off");
        }
    }

    /// <summary>The student list starts from the caller's own active links, so it has nothing to 404.</summary>
    [Fact]
    public async Task TheStudentList_HoldsOnlyTheCallersActiveLinks()
    {
        Assert.Contains(_annId.ToString(), await Client(_coachId).GetStringAsync("/coach/students"));
        foreach (var caller in new[] { _strangerId, _invitedId })
            Assert.DoesNotContain(_annId.ToString(), await Client(caller).GetStringAsync("/coach/students"));

        await Client(_annId).DeleteAsync($"/me/coaches/{_linkId}");
        Assert.DoesNotContain(_annId.ToString(), await Client(_coachId).GetStringAsync("/coach/students"));
    }

    /// <summary>
    /// Regression guard for #194, as RoundOwnershipTests is for #2: reads the routes the app
    /// registers and fails until every coach and feedback route has a probe above.
    /// </summary>
    [Fact]
    public void EveryCoachAndFeedbackRoute_IsProbed()
    {
        var registered = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .SelectMany(e => (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? Array.Empty<string>())
                .Select(method => $"{method} {Normalise(e.RoutePattern.RawText)}"))
            .Where(route => route.Split(' ')[1] is var path && (path.StartsWith("/coach/") || path.StartsWith("/feedback")))
            .ToHashSet();

        var probed = Probes().Select(p => p.Template).Append(StudentList).ToHashSet();
        var unprobed = registered.Except(probed).OrderBy(r => r).ToList();

        Assert.True(unprobed.Count == 0,
            "Coach or feedback routes without an access probe; add them to Probes(): " + string.Join(", ", unprobed));
        // Guards the discovery itself, so the check above can't pass vacuously.
        Assert.Subset(registered, probed);
    }

    private const string StudentList = "GET /coach/students";
    private const string StopCoaching = "DELETE /coach/students/{golferId}";

    /// <summary>
    /// One request per endpoint, and per kind of item for feedback, with what the coach must be
    /// shown to make it. Bodies are valid, so the coach's control request succeeds.
    /// </summary>
    private IEnumerable<Probe> Probes()
    {
        var root = $"/coach/students/{_annId}";
        const string r = "/coach/students/{golferId}";
        var p = CoachShare.Practice;
        var rp = CoachShare.RoundsAndProgress;

        yield return Get($"{r}/overview", rp, $"{root}/overview");
        yield return Get($"{r}/rounds", rp, $"{root}/rounds");
        yield return Get($"{r}/rounds/{{roundId}}", rp, $"{root}/rounds/{_roundId}");
        yield return Get($"{r}/rounds/{{roundId}}/fitness-score", rp, $"{root}/rounds/{_roundId}/fitness-score");
        yield return Get($"{r}/rounds/{{roundId}}/micro-wins", rp, $"{root}/rounds/{_roundId}/micro-wins");
        yield return Get($"{r}/rounds/{{roundId}}/next-lever", rp, $"{root}/rounds/{_roundId}/next-lever");
        yield return Get($"{r}/rounds/{{roundId}}/reflections", CoachShare.Reflections, $"{root}/rounds/{_roundId}/reflections");
        yield return Get($"{r}/timeline", rp, $"{root}/timeline");
        yield return Get($"{r}/timeline/monthly", rp, $"{root}/timeline/monthly");
        yield return Get($"{r}/monthly-reviews/{{monthStart}}", rp, $"{root}/monthly-reviews/2026-10-01");
        yield return Get($"{r}/practice-sessions", p, $"{root}/practice-sessions");
        yield return Get($"{r}/practice-sessions/{{sessionId}}", p, $"{root}/practice-sessions/{_sessionId}");
        yield return Get($"{r}/practice-goals", p, $"{root}/practice-goals");
        yield return Get($"{r}/practice-goals/{{id}}", p, $"{root}/practice-goals/{_goalId}");
        yield return Get($"{r}/practice-goals/{{id}}/outcome", p, $"{root}/practice-goals/{_goalId}/outcome");
        yield return Send(HttpMethod.Post, $"{r}/practice-goals", p, $"{root}/practice-goals", Goal("Bunker play"));
        yield return Send(HttpMethod.Put, $"{r}/practice-goals/{{id}}", p, $"{root}/practice-goals/{_goalId}", Goal("Lag putting, 10m"), changes: true);
        yield return Send(HttpMethod.Delete, $"{r}/practice-goals/{{id}}", p, $"{root}/practice-goals/{_goalId}", changes: true);
        yield return Send(HttpMethod.Delete, r, rp, root, changes: true);

        foreach (var (type, targetId) in Targets())
        {
            var needs = type switch { "Reflection" => CoachShare.Reflections, "PracticeSession" => p, _ => rp };
            var item = new { golferId = _annId, targetType = type, targetId };
            yield return Get("/feedback", needs, $"/feedback?golferId={_annId}&targetType={type}&targetId={targetId}");
            yield return Send(HttpMethod.Post, "/feedback", needs, "/feedback",
                new { golferId = _annId, targetType = type, targetId, text = "Another thought" });
            yield return Send(HttpMethod.Post, "/feedback/read", needs, "/feedback/read", item);
            yield return Send(HttpMethod.Patch, "/feedback/{id}", needs, $"/feedback/{_coachMessages[type]}", new { text = "Edited" }, changes: true);
            yield return Send(HttpMethod.Delete, "/feedback/{id}", needs, $"/feedback/{_coachMessages[type]}", changes: true);
        }
    }

    private IEnumerable<(string Type, Guid TargetId)> Targets() =>
        new[] { ("Round", _roundId), ("Reflection", _reflectionId), ("PracticeSession", _sessionId) };

    private async Task AssertNotFoundAsync(Guid caller, Probe probe)
    {
        var response = await Client(caller).SendAsync(probe.Request());
        Assert.True(response.StatusCode == HttpStatusCode.NotFound, $"{probe} answered {(int)response.StatusCode}");

        var body = await response.Content.ReadAsStringAsync();
        foreach (var secret in new[] { RoundNotes, WhatWorked, CoachNote, "ann@example.test" })
            Assert.DoesNotContain(secret, body);
    }

    /// <summary>
    /// The refused writes left everything as it was: no new goal or message, nothing edited or
    /// deleted. With [share] set, only what that sharing covers is checked.
    /// </summary>
    private async Task AssertNothingChangedAsync(CoachShare? share = null) => await _factory.WithDbAsync(async db =>
    {
        if (share is null or CoachShare.Practice)
        {
            var goal = await db.PracticeGoals.SingleAsync(g => g.GolferId == _annId);
            Assert.Equal("Lag putting", goal.Title);
        }

        var messages = await db.FeedbackMessages.Where(m => m.GolferId == _annId).ToListAsync();
        var covered = _coachMessages
            .Where(kv => share is null
                         || (share == CoachShare.Reflections && kv.Key == "Reflection")
                         || (share == CoachShare.Practice && kv.Key == "PracticeSession"))
            .ToList();
        foreach (var (type, id) in covered)
            Assert.StartsWith(CoachNote, Assert.Single(messages, m => m.FeedbackMessageId == id).Text);
        if (share is null) Assert.Equal(3, messages.Count);
    });

    private static Probe Get(string template, CoachShare needs, string url) =>
        new($"GET {template}", needs, () => new HttpRequestMessage(HttpMethod.Get, url), Changes: false);

    private static Probe Send(HttpMethod method, string template, CoachShare needs, string url, object? body = null, bool changes = false) =>
        new($"{method.Method} {template}", needs, () => new HttpRequestMessage(method, url)
        {
            Content = body is null ? null : JsonContent.Create(body),
        }, changes);

    private static object Goal(string title) =>
        new { title, targetSessions = 6, endsOn = Today.AddDays(28).ToString("yyyy-MM-dd") };

    /// <summary>
    /// Writes a registered route as the probes do: a leading slash and no constraints, e.g.
    /// <c>coach/students/{golferId:guid}</c> becomes <c>/coach/students/{golferId}</c>.
    /// </summary>
    private static string Normalise(string? rawText) =>
        Regex.Replace("/" + (rawText ?? "").TrimStart('/'), @"\{(\w+)(:[^}]*)?\}", "{$1}");

    private HttpClient Client(Guid userId) => TestWorld.ClientFor(_factory, userId);

    /// <param name="Template">The method and route, as <see cref="Normalise"/> writes registered routes.</param>
    /// <param name="Changes">Whether a successful request edits or deletes something.</param>
    private sealed record Probe(string Template, CoachShare Needs, Func<HttpRequestMessage> Request, bool Changes)
    {
        public override string ToString() => Template;
    }

    // Nullable, as a session's practiceGoalId is null when it isn't linked to a goal.
    private sealed record IdPayload(
        Guid? RoundId, Guid? ReflectionId, Guid? PracticeSessionId, Guid? PracticeGoalId, Guid? FeedbackMessageId);
}
