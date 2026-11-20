using GolfProgress.Api.Services;
using GolfProgress.Domain.Entities;
using GolfProgress.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;

namespace GolfProgress.Api.Tests;

/// <summary>
/// Issues #226 and #229: practice goals a coach sets, and the golfer's sessions linked to them.
/// </summary>
public sealed class PracticeGoalTests : IAsyncLifetime
{
    private readonly ApiFactory _factory = new();
    private readonly Guid _coachId = Guid.NewGuid();
    private readonly Guid _otherCoachId = Guid.NewGuid();
    private readonly Guid _annId = Guid.NewGuid();
    private readonly Guid _benId = Guid.NewGuid();

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd");

    public async Task InitializeAsync() => await _factory.WithDbAsync(async db =>
    {
        foreach (var (id, name) in new[] { (_coachId, "Pro"), (_otherCoachId, "Kim"), (_annId, "Ann"), (_benId, "Ben") })
        {
            var user = TestWorld.NewUser(id, $"{name.ToLowerInvariant()}@example.test");
            user.DisplayName = name;
            db.Users.Add(user);
        }
        await db.SaveChangesAsync();
    });

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ACoachSetsAGoal_TheGolferSeesIt_AndLinksSessionsToIt()
    {
        await LinkAsync(_coachId);

        var set = await Coach().PostAsJsonAsync(GoalsUrl(), Goal("  Lag putts inside 1 metre  ", focusArea: "Putting", sessionType: "Putting"));
        Assert.Equal(HttpStatusCode.OK, set.StatusCode);
        var goal = (await set.Content.ReadFromJsonAsync<GoalPayload>())!;
        Assert.Equal("Lag putts inside 1 metre", goal.Title);
        Assert.Equal("Active", goal.Status);
        Assert.Equal(Iso(Today), goal.StartsOn);

        var mine = Assert.Single((await Golfer().GetFromJsonAsync<List<GoalPayload>>("/me/practice-goals"))!);
        Assert.Equal("Pro", mine.CoachName);
        Assert.Equal(0, mine.LinkedSessions);

        var session = await PracticeAsync(goal.PracticeGoalId, Today);
        Assert.Equal(goal.PracticeGoalId, session.PracticeGoalId);

        var detail = (await Golfer().GetFromJsonAsync<DetailPayload>($"/me/practice-goals/{goal.PracticeGoalId}"))!;
        Assert.Equal(1, detail.Goal.LinkedSessions);
        Assert.Equal(session.PracticeSessionId, Assert.Single(detail.Sessions).PracticeSessionId);
        var coachView = (await Coach().GetFromJsonAsync<DetailPayload>($"{GoalsUrl()}/{goal.PracticeGoalId}"))!;
        Assert.Equal(1, coachView.Goal.LinkedSessions);

        await _factory.WithDbAsync(async db =>
            Assert.True(await db.Events.AnyAsync(e => e.UserId == _annId && e.EventType == "PracticeGoalSet")));
    }

    [Fact]
    public async Task AGoal_IsValidated()
    {
        await LinkAsync(_coachId);

        var invalid = new object[]
        {
            Goal(" "),
            Goal(new string('t', 101)),
            Goal("Putts", target: 0),
            Goal("Putts", target: 51),
            Goal("Putts", endsOn: Today.AddDays(57)),
            Goal("Putts", startsOn: Today.AddDays(5), endsOn: Today.AddDays(2)),
            Goal("Putts", startsOn: Today.AddDays(-20), endsOn: Today.AddDays(-1)),
            Goal("Putts", focusArea: "Chipping"),
            Goal("Putts", sessionType: "Yoga"),
            new { title = "Putts", targetSessions = 4, endsOn = "next week" },
        };
        foreach (var body in invalid)
            Assert.Equal(HttpStatusCode.BadRequest, (await Coach().PostAsJsonAsync(GoalsUrl(), body)).StatusCode);

        Assert.Equal(HttpStatusCode.OK,
            (await Coach().PostAsJsonAsync(GoalsUrl(), Goal(new string('t', 100), target: 50, endsOn: Today.AddDays(56)))).StatusCode);
    }

    [Fact]
    public async Task WithoutPracticeShared_OrAnActiveLink_CoachRoutesAre404()
    {
        await LinkAsync(_coachId, sharePractice: false);
        await LinkAsync(_otherCoachId, endedAt: DateTimeOffset.UtcNow.AddMinutes(-1));
        var goalId = await SeedGoalAsync(_coachId);

        foreach (var client in new[] { Coach(), TestWorld.ClientFor(_factory, _otherCoachId), TestWorld.ClientFor(_factory, _benId) })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(GoalsUrl())).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{GoalsUrl()}/{goalId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(GoalsUrl(), Goal("Putts"))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"{GoalsUrl()}/{goalId}", Goal("Putts"))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"{GoalsUrl()}/{goalId}")).StatusCode);
        }
        // Not shared, so not on the overview either.
        var overview = await Coach().GetFromJsonAsync<OverviewPayload>($"/coach/students/{_annId}/overview");
        Assert.Empty(overview!.ActiveGoals);
    }

    [Fact]
    public async Task ACoach_OnlyReachesTheGoalsTheySet_AndTheGolferSeesEveryCoachs()
    {
        await LinkAsync(_coachId);
        await LinkAsync(_otherCoachId);
        var mine = await SeedGoalAsync(_coachId, "Pro's goal");
        var theirs = await SeedGoalAsync(_otherCoachId, "Kim's goal");

        Assert.Equal(new[] { "Pro's goal" }, (await Coach().GetFromJsonAsync<List<GoalPayload>>(GoalsUrl()))!.Select(g => g.Title));
        Assert.Equal(HttpStatusCode.NotFound, (await Coach().GetAsync($"{GoalsUrl()}/{theirs}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Coach().PutAsJsonAsync($"{GoalsUrl()}/{theirs}", Goal("Mine now"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Coach().DeleteAsync($"{GoalsUrl()}/{theirs}")).StatusCode);
        var overview = await Coach().GetFromJsonAsync<OverviewPayload>($"/coach/students/{_annId}/overview");
        Assert.Equal(mine, Assert.Single(overview!.ActiveGoals).PracticeGoalId);

        Assert.Equal(2, (await Golfer().GetFromJsonAsync<List<GoalPayload>>("/me/practice-goals"))!.Count);
    }

    [Fact]
    public async Task TheCoachChangesAGoal_AndCanRemoveIt_OnlyWhileNoSessionIsLinked()
    {
        await LinkAsync(_coachId);
        var goalId = await SeedGoalAsync(_coachId);

        var changed = await Coach().PutAsJsonAsync($"{GoalsUrl()}/{goalId}", Goal("Lag putts", target: 8, focusArea: "Putting"));
        var goal = (await changed.Content.ReadFromJsonAsync<GoalPayload>())!;
        Assert.Equal("Lag putts", goal.Title);
        Assert.Equal(8, goal.TargetSessions);
        Assert.NotNull(goal.UpdatedAt);

        await PracticeAsync(goalId, Today);
        var refused = await Coach().DeleteAsync($"{GoalsUrl()}/{goalId}");
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);

        var unlinked = await SeedGoalAsync(_coachId, "Another");
        Assert.Equal(HttpStatusCode.NoContent, (await Coach().DeleteAsync($"{GoalsUrl()}/{unlinked}")).StatusCode);
    }

    [Fact]
    public async Task ASession_LinksOnlyToTheGolfersOwnActiveGoal_WithinItsPeriod()
    {
        await LinkAsync(_coachId);
        var goalId = await SeedGoalAsync(_coachId, startsOn: Today.AddDays(-3), endsOn: Today.AddDays(3));
        var done = await SeedGoalAsync(_coachId, status: PracticeGoalStatus.Done);
        var bensGoal = await SeedGoalAsync(_coachId, golferId: _benId);

        foreach (var (goal, date) in new[]
                 {
                     (goalId, Today.AddDays(-4)), (goalId, Today.AddDays(4)), (done, Today), (bensGoal, Today), (Guid.NewGuid(), Today),
                 })
        {
            var response = await Golfer().PostAsJsonAsync("/me/practice-sessions",
                new { practicedAt = Iso(date), sessionType = "Putting", practiceGoalId = goal });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        Assert.Equal(Today.AddDays(-3), DateOnly.Parse((await PracticeAsync(goalId, Today.AddDays(-3))).PracticedAt));
    }

    [Fact]
    public async Task TheGolfer_MarksAGoalDone_OrDismissesIt()
    {
        await LinkAsync(_coachId);
        var goalId = await SeedGoalAsync(_coachId);
        var other = await SeedGoalAsync(_coachId);

        var done = await Golfer().PatchAsJsonAsync($"/me/practice-goals/{goalId}", new { status = "Done" });
        Assert.Equal("Done", (await done.Content.ReadFromJsonAsync<GoalPayload>())!.Status);
        var dismissed = await Golfer().PatchAsJsonAsync($"/me/practice-goals/{other}", new { status = "dismissed" });
        Assert.Equal("Dismissed", (await dismissed.Content.ReadFromJsonAsync<GoalPayload>())!.Status);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await Golfer().PatchAsJsonAsync($"/me/practice-goals/{goalId}", new { status = "Active" })).StatusCode);
        // Nobody else can, the coach included.
        Assert.Equal(HttpStatusCode.NotFound,
            (await TestWorld.ClientFor(_factory, _benId).PatchAsJsonAsync($"/me/practice-goals/{goalId}", new { status = "Done" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await Coach().PatchAsJsonAsync($"/me/practice-goals/{goalId}", new { status = "Done" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await TestWorld.ClientFor(_factory, _benId).GetAsync($"/me/practice-goals/{goalId}")).StatusCode);
    }

    [Fact]
    public async Task AGoalPastItsPeriod_ReadsAsEnded_AndComesAfterCurrentOnes()
    {
        await LinkAsync(_coachId);
        await SeedGoalAsync(_coachId, "Old", startsOn: Today.AddDays(-30), endsOn: Today.AddDays(-2));
        await SeedGoalAsync(_coachId, "Current");

        var goals = (await Golfer().GetFromJsonAsync<List<GoalPayload>>("/me/practice-goals"))!;
        Assert.Equal(new[] { "Current", "Old" }, goals.Select(g => g.Title));
        Assert.Equal("Ended", goals[1].Status);
    }

    [Fact]
    public async Task ADeletedCoachsGoals_StayWithTheGolfer()
    {
        await LinkAsync(_coachId);
        await SeedGoalAsync(_coachId, "Keep this");

        await _factory.WithDbAsync(async db =>
            Assert.True(await new AccountDeletionService(db).DeleteAsync(_coachId, CancellationToken.None)));

        var goal = Assert.Single((await Golfer().GetFromJsonAsync<List<GoalPayload>>("/me/practice-goals"))!);
        Assert.Equal("Keep this", goal.Title);
        Assert.Null(goal.CoachId);
        Assert.Null(goal.CoachName);
    }

    [Fact]
    public async Task EveryEndpoint_NeedsASignedInUser()
    {
        var anonymous = _factory.CreateClient();
        var id = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/me/practice-goals")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/me/practice-goals/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PatchAsJsonAsync($"/me/practice-goals/{id}", new { status = "Done" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(GoalsUrl())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync(GoalsUrl(), Goal("Putts"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PutAsJsonAsync($"{GoalsUrl()}/{id}", Goal("Putts"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.DeleteAsync($"{GoalsUrl()}/{id}")).StatusCode);
    }

    private HttpClient Coach() => TestWorld.ClientFor(_factory, _coachId);
    private HttpClient Golfer() => TestWorld.ClientFor(_factory, _annId);
    private string GoalsUrl() => $"/coach/students/{_annId}/practice-goals";

    private static object Goal(
        string title, int target = 6, DateOnly? startsOn = null, DateOnly? endsOn = null,
        string? focusArea = null, string? sessionType = null) =>
        new
        {
            title,
            targetSessions = target,
            startsOn = startsOn is { } s ? Iso(s) : null,
            endsOn = Iso(endsOn ?? Today.AddDays(28)),
            focusArea,
            sessionType,
        };

    private async Task<SessionPayload> PracticeAsync(Guid goalId, DateOnly date)
    {
        var response = await Golfer().PostAsJsonAsync("/me/practice-sessions",
            new { practicedAt = Iso(date), sessionType = "Putting", practiceGoalId = goalId });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SessionPayload>())!;
    }

    private async Task<Guid> SeedGoalAsync(
        Guid coachId, string title = "Putting", DateOnly? startsOn = null, DateOnly? endsOn = null,
        PracticeGoalStatus status = PracticeGoalStatus.Active, Guid? golferId = null)
    {
        var id = Guid.NewGuid();
        await _factory.WithDbAsync(async db =>
        {
            db.PracticeGoals.Add(new PracticeGoal
            {
                PracticeGoalId = id,
                GolferId = golferId ?? _annId,
                CoachId = coachId,
                Title = title,
                TargetSessions = 6,
                StartsOn = startsOn ?? Today,
                EndsOn = endsOn ?? Today.AddDays(28),
                Status = status,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        });
        // Created after the previous one, so "newest first" is well defined.
        await Task.Delay(5);
        return id;
    }

    private async Task LinkAsync(Guid coachId, bool sharePractice = true, DateTimeOffset? endedAt = null) =>
        await _factory.WithDbAsync(async db =>
        {
            db.CoachLinks.Add(new CoachLink
            {
                CoachLinkId = Guid.NewGuid(),
                GolferId = _annId,
                CoachId = coachId,
                ShareReflections = true,
                SharePractice = sharePractice,
                CreatedAt = DateTimeOffset.UtcNow,
                EndedAt = endedAt,
            });
            await db.SaveChangesAsync();
        });

    private sealed record GoalPayload(
        Guid PracticeGoalId, Guid? CoachId, string? CoachName, string Title, int TargetSessions, int LinkedSessions,
        string StartsOn, string EndsOn, string Status, DateTimeOffset? UpdatedAt);

    private sealed record SessionPayload(Guid PracticeSessionId, string PracticedAt, Guid? PracticeGoalId);

    private sealed record DetailPayload(GoalPayload Goal, List<SessionPayload> Sessions);

    private sealed record OverviewPayload(List<GoalPayload> ActiveGoals);
}
