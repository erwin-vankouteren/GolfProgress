using GolfProgress.Api.Configuration;
using GolfProgress.Api.Security;
using GolfProgress.Api.Services;
using GolfProgress.Domain.Entities;
using GolfProgress.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GolfProgress.Api.Tests;

/// <summary>The rules of a user's plan (#309, docs/payments.md → "Decisions"), apart from the API.</summary>
public sealed class PlanRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 12, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly PaymentsOptions Launched = new() { LaunchDate = Now.AddDays(-40) };

    [Fact]
    public void BeforeLaunch_EveryoneHasEverything_WithoutATrialEnd()
    {
        var plan = PlanService.Compute(Now.AddYears(-1), [], new PaymentsOptions(), Now);

        Assert.Equal(Plan.Trial, plan.Plan);
        Assert.Null(plan.TrialEndsAt);
        Assert.True(plan.Coaching);
        Assert.True(plan.CanLog);
    }

    [Fact]
    public void ANewAccount_HasThirtyDaysFromSigningUp()
    {
        var signedUp = Now.AddDays(-10);
        var plan = PlanService.Compute(signedUp, [], Launched, Now);

        Assert.Equal(Plan.Trial, plan.Plan);
        Assert.Equal(signedUp.AddDays(30), plan.TrialEndsAt);
        Assert.True(plan.Coaching);
    }

    [Fact]
    public void AnAccountFromBeforeLaunch_GetsThirtyDaysFromLaunch()
    {
        var launch = Now.AddDays(-5);
        var plan = PlanService.Compute(Now.AddYears(-1), [], new PaymentsOptions { LaunchDate = launch }, Now);

        Assert.Equal(Plan.Trial, plan.Plan);
        Assert.Equal(launch.AddDays(30), plan.TrialEndsAt);
    }

    [Fact]
    public void AfterTheTrial_WithoutASubscription_ItsReadOnly_AndCoachingStops()
    {
        var plan = PlanService.Compute(Now.AddDays(-60), [], Launched, Now);

        Assert.Equal(Plan.ReadOnly, plan.Plan);
        Assert.False(plan.CanLog);
        Assert.False(plan.Coaching);
    }

    [Theory]
    [InlineData(SubscriptionStatus.Active, Plan.Subscribed)]
    [InlineData(SubscriptionStatus.InGracePeriod, Plan.Subscribed)]
    [InlineData(SubscriptionStatus.OnHold, Plan.ReadOnly)]
    [InlineData(SubscriptionStatus.Cancelled, Plan.ReadOnly)]
    [InlineData(SubscriptionStatus.Expired, Plan.ReadOnly)]
    public void ASubscription_GivesAccess_WhileActiveOrInGracePeriod(SubscriptionStatus status, Plan expected)
    {
        var plan = PlanService.Compute(Now.AddDays(-60), [Row(SubscriptionProduct.Premium, status)], Launched, Now);

        Assert.Equal(expected, plan.Plan);
    }

    [Fact]
    public void Coaching_AfterTheTrial_NeedsTheAddOn_AlongsideTheSubscription()
    {
        var premium = Row(SubscriptionProduct.Premium);
        var addOn = Row(SubscriptionProduct.Coaching);
        var old = Now.AddDays(-60);

        Assert.False(PlanService.Compute(old, [premium], Launched, Now).Coaching);
        Assert.True(PlanService.Compute(old, [premium, addOn], Launched, Now).Coaching);
        // The add-on on its own gives nothing.
        var addOnOnly = PlanService.Compute(old, [addOn], Launched, Now);
        Assert.Equal(Plan.ReadOnly, addOnOnly.Plan);
        Assert.False(addOnOnly.Coaching);
    }

    [Fact]
    public void SubscribingDuringTheTrial_KeepsCoachingUntilTheTrialEnds()
    {
        var plan = PlanService.Compute(Now.AddDays(-10), [Row(SubscriptionProduct.Premium)], Launched, Now);

        Assert.Equal(Plan.Subscribed, plan.Plan);
        Assert.True(plan.Coaching);
    }

    private static Subscription Row(SubscriptionProduct product, SubscriptionStatus status = SubscriptionStatus.Active) => new()
    {
        SubscriptionId = Guid.NewGuid(),
        Source = SubscriptionSource.Stripe,
        ExternalId = "sub_1",
        Product = product,
        Period = SubscriptionPeriod.Month,
        Status = status,
        CurrentPeriodEnd = Now.AddDays(20),
    };
}

/// <summary>
/// Issue #309: the plan on GET /me, and read-only after the trial. Every write to the
/// golfer's rounds, reflections, practice and reviews answers 402; reading, the profile and
/// deleting the account keep working.
/// </summary>
public sealed class PlanTests : IAsyncLifetime
{
    private readonly ApiFactory _base = new();
    private WebApplicationFactory<Program> _factory = null!;
    private readonly Guid _annId = Guid.NewGuid();
    private readonly Guid _benId = Guid.NewGuid();
    private readonly Guid _roundId = Guid.NewGuid();
    private readonly Guid _sessionId = Guid.NewGuid();
    private static readonly DateTimeOffset LaunchDate = DateTimeOffset.UtcNow.AddDays(-90);

    public async Task InitializeAsync()
    {
        _factory = _base.WithWebHostBuilder(b =>
            b.ConfigureServices(s => s.PostConfigure<PaymentsOptions>(o => o.LaunchDate = LaunchDate)));

        await _base.WithDbAsync(async db =>
        {
            // Ann signed up long ago and never subscribed; Ben pays monthly.
            var ann = TestWorld.NewUser(_annId, "ann@example.test");
            ann.CreatedAt = DateTimeOffset.UtcNow.AddDays(-120);
            var ben = TestWorld.NewUser(_benId, "ben@example.test");
            ben.CreatedAt = DateTimeOffset.UtcNow.AddDays(-120);
            db.Users.AddRange(ann, ben);
            db.Rounds.Add(TestWorld.NewRound(_roundId, _annId));
            db.PracticeSessions.Add(new PracticeSession
            {
                PracticeSessionId = _sessionId, UserId = _annId, PracticedAt = new DateOnly(2026, 10, 10),
                SessionType = SessionType.Putting, CreatedAt = DateTimeOffset.UtcNow,
            });
            db.Subscriptions.Add(new Subscription
            {
                SubscriptionId = Guid.NewGuid(), UserId = _benId, Source = SubscriptionSource.Stripe, ExternalId = "sub_ben",
                Product = SubscriptionProduct.Premium, Period = SubscriptionPeriod.Month, Status = SubscriptionStatus.Active,
                CurrentPeriodEnd = DateTimeOffset.UtcNow.AddDays(12), UpdatedAt = DateTimeOffset.UtcNow,
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
    public async Task BeforeLaunch_MeSaysTrial_WithoutAnEnd()
    {
        var me = await MeAsync(TestWorld.ClientFor(_base, _annId));

        Assert.Equal("Trial", me.GetProperty("plan").GetString());
        Assert.Equal(JsonValueKind.Null, me.GetProperty("trialEndsAt").ValueKind);
        Assert.True(me.GetProperty("coaching").GetBoolean());
        Assert.Equal(JsonValueKind.Null, me.GetProperty("subscription").ValueKind);
    }

    [Fact]
    public async Task AfterTheTrial_MeSaysReadOnly_WithWhenItEnded()
    {
        var me = await MeAsync(Client(_annId));

        Assert.Equal("ReadOnly", me.GetProperty("plan").GetString());
        Assert.Equal(LaunchDate.AddDays(30), me.GetProperty("trialEndsAt").GetDateTimeOffset());
        Assert.False(me.GetProperty("coaching").GetBoolean());
    }

    [Fact]
    public async Task ASubscriber_SeesTheirSubscription()
    {
        var me = await MeAsync(Client(_benId));

        Assert.Equal("Subscribed", me.GetProperty("plan").GetString());
        var subscription = me.GetProperty("subscription");
        Assert.Equal("Stripe", subscription.GetProperty("source").GetString());
        Assert.Equal("Month", subscription.GetProperty("period").GetString());
        Assert.Equal(JsonValueKind.Null, subscription.GetProperty("cancelsAt").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, subscription.GetProperty("renewsAt").ValueKind);
        Assert.False(subscription.GetProperty("coachingAddOn").GetBoolean());
    }

    [Fact]
    public async Task ReadOnly_EveryWriteIs402_AndChangesNothing()
    {
        foreach (var write in Writes())
        {
            var response = await Client(_annId).SendAsync(write());
            Assert.True(response.StatusCode == HttpStatusCode.PaymentRequired,
                $"{write().Method} {write().RequestUri} answered {(int)response.StatusCode}");
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(RequiresSubscriptionAttribute.ProblemType, problem.GetProperty("type").GetString());
        }

        await _base.WithDbAsync(async db =>
        {
            Assert.Equal(1, await db.Rounds.CountAsync(r => r.UserId == _annId));
            Assert.Equal(1, await db.PracticeSessions.CountAsync(s => s.UserId == _annId));
            Assert.Equal(0, await db.Reflections.CountAsync(r => r.UserId == _annId));
            Assert.Equal(0, await db.MonthlyReviews.CountAsync(r => r.UserId == _annId));
        });
    }

    [Fact]
    public async Task ReadOnly_ReadingTheProfileAndDeletingStillWork()
    {
        var ann = Client(_annId);

        Assert.Equal(HttpStatusCode.OK, (await ann.GetAsync("/me/rounds")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ann.GetAsync($"/rounds/{_roundId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ann.GetAsync("/me/practice-sessions")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ann.GetAsync("/me/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ann.PatchAsJsonAsync("/me", new { displayName = "Ann B" })).StatusCode);
        // Deleting asks for the password, not a subscription: a wrong one is a 400, not a 402.
        var delete = new HttpRequestMessage(HttpMethod.Delete, "/me") { Content = JsonContent.Create(new { password = "wrong" }) };
        Assert.Equal(HttpStatusCode.BadRequest, (await ann.SendAsync(delete)).StatusCode);
    }

    [Fact]
    public async Task ASubscriber_LogsAsBefore()
    {
        var response = await Client(_benId).PostAsJsonAsync("/me/rounds", new { playedAt = "2026-11-01", holes = 18, totalStrokes = 90 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    /// <summary>
    /// Regression guard, as RoundOwnershipTests is for #2: every write to the golfer's own
    /// golf carries [RequiresSubscription], or is listed here with the reason it doesn't.
    /// </summary>
    [Fact]
    public void EveryWriteToTheGolfersGolf_NeedsASubscription()
    {
        string[] golf = ["/me/rounds", "/rounds/", "/me/practice-sessions", "/me/monthly-reviews"];
        string[] exempt =
        [
            // Derive from what's already there, and add nothing the golfer enters.
            "POST /rounds/{roundId}/compute",
            "POST /rounds/{roundId}/micro-wins/generate",
            "POST /rounds/{roundId}/next-lever",
        ];

        var writes = _base.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .SelectMany(e => (e.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()?.HttpMethods ?? [])
                .Where(m => m is not "GET" and not "HEAD")
                .Select(m => (Route: $"{m} {Regex.Replace("/" + (e.RoutePattern.RawText ?? "").TrimStart('/'), @"\{(\w+)(:[^}]*)?\}", "{$1}")}",
                    Guarded: e.Metadata.GetMetadata<RequiresSubscriptionAttribute>() is not null)))
            .Where(w => golf.Any(prefix => w.Route.Split(' ')[1].StartsWith(prefix)))
            .ToList();

        var unguarded = writes.Where(w => !w.Guarded && !exempt.Contains(w.Route)).Select(w => w.Route).OrderBy(r => r).ToList();
        Assert.True(unguarded.Count == 0, "Writes without [RequiresSubscription]: " + string.Join(", ", unguarded));
        // The discovery found the seven it should, so the check above can't pass vacuously.
        Assert.Equal(7, writes.Count(w => w.Guarded));
    }

    private IEnumerable<Func<HttpRequestMessage>> Writes()
    {
        var round = new { playedAt = "2026-11-01", holes = 18, totalStrokes = 90 };
        yield return () => Json(HttpMethod.Post, "/me/rounds", round);
        yield return () => Json(HttpMethod.Put, $"/rounds/{_roundId}", round);
        yield return () => new HttpRequestMessage(HttpMethod.Delete, $"/rounds/{_roundId}");
        yield return () => Json(HttpMethod.Post, $"/rounds/{_roundId}/reflections", new { focusRating = 4 });
        yield return () => Json(HttpMethod.Post, "/me/practice-sessions",
            new { practicedAt = "2026-11-01", sessionType = "Putting", durationMinutes = 30 });
        yield return () => new HttpRequestMessage(HttpMethod.Delete, $"/me/practice-sessions/{_sessionId}");
        yield return () => Json(HttpMethod.Post, "/me/monthly-reviews/generate", new { monthStart = "2026-10-01" });
    }

    private static HttpRequestMessage Json(HttpMethod method, string url, object body) =>
        new(method, url) { Content = JsonContent.Create(body) };

    private HttpClient Client(Guid userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestWorld.TokenFor(userId));
        return client;
    }

    private static async Task<JsonElement> MeAsync(HttpClient client) =>
        await client.GetFromJsonAsync<JsonElement>("/me");
}
