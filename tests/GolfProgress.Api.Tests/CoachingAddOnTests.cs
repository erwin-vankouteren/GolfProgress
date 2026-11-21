using GolfProgress.Api.Configuration;
using GolfProgress.Api.Security;
using GolfProgress.Domain.Entities;
using GolfProgress.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace GolfProgress.Api.Tests;

/// <summary>
/// Issue #312: Coaching as a golfer's add-on (docs/payments.md → "What each state allows").
/// Without it, after the trial, their coaches' links are paused: the coach's routes answer
/// 404, the Students list marks them paused, and the golfer can't invite a coach or write to
/// one. Adding Coaching resumes everything, with nothing lost.
/// </summary>
public sealed class CoachingAddOnTests : IAsyncLifetime
{
    private readonly ApiFactory _base = new();
    private WebApplicationFactory<Program> _factory = null!;
    private readonly Guid _annId = Guid.NewGuid();
    private readonly Guid _coachId = Guid.NewGuid();
    private readonly Guid _roundId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        _factory = _base.WithWebHostBuilder(b => b.ConfigureServices(s =>
            s.PostConfigure<PaymentsOptions>(o => o.LaunchDate = DateTimeOffset.UtcNow.AddDays(-90))));

        await _base.WithDbAsync(async db =>
        {
            // Ann's trial ended long ago; her coach never subscribed, which doesn't matter.
            var ann = TestWorld.NewUser(_annId, "ann@example.test");
            ann.CreatedAt = DateTimeOffset.UtcNow.AddDays(-120);
            var coach = TestWorld.NewUser(_coachId, "coach@example.test");
            coach.CreatedAt = DateTimeOffset.UtcNow.AddDays(-120);
            db.Users.AddRange(ann, coach);
            db.Rounds.Add(TestWorld.NewRound(_roundId, _annId));
            db.CoachLinks.Add(new CoachLink
            {
                CoachLinkId = Guid.NewGuid(), GolferId = _annId, CoachId = _coachId,
                ShareReflections = true, SharePractice = true, CreatedAt = DateTimeOffset.UtcNow.AddDays(-100),
            });
            await db.SaveChangesAsync();
        });
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        _base.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task WithoutCoaching_TheCoachsRoutesAre404_AndTheStudentIsPaused()
    {
        var coach = Client(_coachId);

        Assert.Equal(HttpStatusCode.NotFound, (await coach.GetAsync($"/coach/students/{_annId}/overview")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await coach.GetAsync($"/coach/students/{_annId}/rounds/{_roundId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await coach.PostAsJsonAsync("/feedback", new { golferId = _annId, targetType = "Round", targetId = _roundId, text = "Nice" })).StatusCode);

        var student = Assert.Single(await coach.GetFromJsonAsync<List<JsonElement>>("/coach/students") ?? []);
        Assert.True(student.GetProperty("paused").GetBoolean());
        Assert.Equal(JsonValueKind.Null, student.GetProperty("lastActivityOn").ValueKind);

        // The coach can still end it.
        Assert.Equal(HttpStatusCode.NoContent, (await coach.DeleteAsync($"/coach/students/{_annId}")).StatusCode);
        Assert.Empty(await coach.GetFromJsonAsync<List<JsonElement>>("/coach/students") ?? []);
    }

    [Fact]
    public async Task TheSubscriptionAlone_DoesntResumeIt_TheAddOnDoes()
    {
        await SubscribeAsync(SubscriptionProduct.Premium);
        Assert.Equal(HttpStatusCode.NotFound, (await Client(_coachId).GetAsync($"/coach/students/{_annId}/overview")).StatusCode);

        await SubscribeAsync(SubscriptionProduct.Coaching);
        Assert.Equal(HttpStatusCode.OK, (await Client(_coachId).GetAsync($"/coach/students/{_annId}/overview")).StatusCode);
        var student = Assert.Single(await Client(_coachId).GetFromJsonAsync<List<JsonElement>>("/coach/students") ?? []);
        Assert.False(student.GetProperty("paused").GetBoolean());
    }

    [Fact]
    public async Task WithoutCoaching_TheGolferCantInviteOrWriteToACoach_ButStillReadsTheirFeedback()
    {
        var ann = Client(_annId);

        var invite = await ann.PostAsJsonAsync("/me/coach-invitations", new { email = "pro@example.test", shareReflections = true, sharePractice = true });
        Assert.Equal(HttpStatusCode.PaymentRequired, invite.StatusCode);
        Assert.Equal(RequiresSubscriptionAttribute.CoachingProblemType,
            (await invite.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("type").GetString());

        var write = await ann.PostAsJsonAsync("/feedback",
            new { golferId = _annId, targetType = "Round", targetId = _roundId, coachId = _coachId, text = "Question" });
        Assert.Equal(HttpStatusCode.PaymentRequired, write.StatusCode);

        Assert.Equal(HttpStatusCode.OK,
            (await ann.GetAsync($"/feedback?golferId={_annId}&targetType=Round&targetId={_roundId}")).StatusCode);
        Assert.False((await ann.GetFromJsonAsync<JsonElement>("/me")).GetProperty("coaching").GetBoolean());
    }

    private Task SubscribeAsync(SubscriptionProduct product) => _base.WithDbAsync(async db =>
    {
        db.Subscriptions.Add(new Subscription
        {
            SubscriptionId = Guid.NewGuid(), UserId = _annId, Source = SubscriptionSource.Stripe, ExternalId = "sub_ann",
            Product = product, Period = SubscriptionPeriod.Month, Status = SubscriptionStatus.Active,
            CurrentPeriodEnd = DateTimeOffset.UtcNow.AddDays(20), UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    });

    private HttpClient Client(Guid userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestWorld.TokenFor(userId));
        return client;
    }
}
