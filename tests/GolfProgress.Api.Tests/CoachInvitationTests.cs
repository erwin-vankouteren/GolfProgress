using GolfProgress.Api.Email;
using GolfProgress.Api.Services;
using GolfProgress.Domain.Enums;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace GolfProgress.Api.Tests;

/// <summary>
/// Issue #184: a golfer invites a coach by email; the coach, signed in with that address,
/// accepts or declines through a single-use link that expires.
/// </summary>
public sealed class CoachInvitationTests : IAsyncLifetime
{
    private ApiFactory _base = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private readonly RecordingOutbox _outbox = new();

    private readonly Guid _golferId = Guid.NewGuid();
    private readonly Guid _coachId = Guid.NewGuid();
    private readonly Guid _strangerId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        _base = new ApiFactory();
        _factory = _base.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IEmailOutbox>();
            s.AddSingleton<IEmailOutbox>(_outbox);
        }));

        await _base.WithDbAsync(async db =>
        {
            var golfer = TestWorld.NewUser(_golferId, "golfer@example.test");
            golfer.DisplayName = "Sam";
            golfer.Language = AppLanguage.Dutch;
            db.Users.Add(golfer);
            db.Users.Add(TestWorld.NewUser(_coachId, "coach@example.test"));
            db.Users.Add(TestWorld.NewUser(_strangerId, "stranger@example.test"));
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
    public async Task TheCoach_GetsALink_AndAcceptingIt_GivesThemAccess()
    {
        var invited = await InviteAsync(" Coach@Example.test ", shareReflections: false);

        Assert.Equal(HttpStatusCode.OK, invited.StatusCode);
        var invitation = (await invited.Content.ReadFromJsonAsync<InvitationPayload>())!;
        Assert.Equal("coach@example.test", invitation.Email);
        Assert.False(invitation.ShareReflections);
        Assert.True(invitation.SharePractice);
        Assert.False(invitation.Resent);

        // In the golfer's language, with their name and what the coach will see.
        var email = Assert.Single(_outbox.Sent);
        Assert.Equal("coach@example.test", email.To);
        Assert.Equal("Sam heeft je uitgenodigd als coach op GolfProgress", email.Subject);
        Assert.Contains("de rondes en voortgang en de trainingen van Sam", email.PlainText);
        Assert.Contains("http://localhost:5173/coach-invitation?token=", email.PlainText);

        var accepted = await Client(_coachId).PostAsJsonAsync("/coach-invitations/accept", new { token = TokenFrom(email) });

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var link = (await accepted.Content.ReadFromJsonAsync<AcceptedPayload>())!;
        Assert.Equal(_golferId, link.GolferId);
        Assert.Equal("Sam", link.GolferDisplayName);
        Assert.False(link.ShareReflections);
        Assert.True(link.SharePractice);

        await _base.WithDbAsync(async db =>
        {
            var stored = await db.CoachLinks.SingleAsync();
            Assert.Equal((_golferId, _coachId), (stored.GolferId, stored.CoachId));
            Assert.Null(stored.EndedAt);
            Assert.Equal(
                new[] { "CoachInvitationAccepted", "CoachInvited" },
                await db.Events.Where(e => e.UserId == _golferId).Select(e => e.EventType).OrderBy(t => t).ToListAsync());
        });
    }

    [Fact]
    public async Task ALink_WorksOnce()
    {
        await InviteAsync("coach@example.test");
        var token = TokenFrom(_outbox.Sent.Single());

        await Client(_coachId).PostAsJsonAsync("/coach-invitations/accept", new { token });
        var again = await Client(_coachId).PostAsJsonAsync("/coach-invitations/accept", new { token });

        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
    }

    [Fact]
    public async Task Declining_GivesNoAccess_AndEndsTheLink()
    {
        await InviteAsync("coach@example.test");
        var token = TokenFrom(_outbox.Sent.Single());

        var declined = await Client(_coachId).PostAsJsonAsync("/coach-invitations/decline", new { token });
        var accepted = await Client(_coachId).PostAsJsonAsync("/coach-invitations/accept", new { token });

        Assert.Equal(HttpStatusCode.NoContent, declined.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, accepted.StatusCode);
        await _base.WithDbAsync(async db => Assert.Empty(await db.CoachLinks.ToListAsync()));
    }

    [Fact]
    public async Task AnotherAccount_CantUseTheLink_AndTheCoachStillCan()
    {
        await InviteAsync("coach@example.test");
        var token = TokenFrom(_outbox.Sent.Single());

        var forwarded = await Client(_strangerId).PostAsJsonAsync("/coach-invitations/accept", new { token });
        var declinedByStranger = await Client(_strangerId).PostAsJsonAsync("/coach-invitations/decline", new { token });
        var accepted = await Client(_coachId).PostAsJsonAsync("/coach-invitations/accept", new { token });

        Assert.Equal(HttpStatusCode.Forbidden, forwarded.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, declinedByStranger.StatusCode);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    }

    [Fact]
    public async Task UnknownAndExpiredLinks_Are404()
    {
        await InviteAsync("coach@example.test");
        var token = TokenFrom(_outbox.Sent.Single());
        await _base.WithDbAsync(async db =>
        {
            (await db.CoachInvitations.SingleAsync()).ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        });

        var expired = await Client(_coachId).PostAsJsonAsync("/coach-invitations/accept", new { token });
        var unknown = await Client(_coachId).PostAsJsonAsync("/coach-invitations/accept", new { token = "not-a-token" });

        Assert.Equal(HttpStatusCode.NotFound, expired.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task TheGolfer_CanCancel_AndNobodyElseCan()
    {
        var invitation = (await (await InviteAsync("coach@example.test")).Content.ReadFromJsonAsync<InvitationPayload>())!;
        var token = TokenFrom(_outbox.Sent.Single());

        var byStranger = await Client(_strangerId).DeleteAsync($"/me/coach-invitations/{invitation.CoachInvitationId}");
        var cancelled = await Client(_golferId).DeleteAsync($"/me/coach-invitations/{invitation.CoachInvitationId}");
        var again = await Client(_golferId).DeleteAsync($"/me/coach-invitations/{invitation.CoachInvitationId}");
        var accepted = await Client(_coachId).PostAsJsonAsync("/coach-invitations/accept", new { token });

        Assert.Equal(HttpStatusCode.NotFound, byStranger.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, cancelled.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, accepted.StatusCode);
    }

    [Fact]
    public async Task InvitingAgain_ResendsWithANewLink_AndTheOldOneStopsWorking()
    {
        await InviteAsync("coach@example.test");
        var again = await InviteAsync("coach@example.test", sharePractice: false);

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.True((await again.Content.ReadFromJsonAsync<InvitationPayload>())!.Resent);
        var (first, second) = (TokenFrom(_outbox.Sent.First()), TokenFrom(_outbox.Sent.Last()));
        Assert.NotEqual(first, second);
        Assert.Equal(HttpStatusCode.NotFound,
            (await Client(_coachId).PostAsJsonAsync("/coach-invitations/accept", new { token = first })).StatusCode);

        var accepted = await Client(_coachId).PostAsJsonAsync("/coach-invitations/accept", new { token = second });
        Assert.False((await accepted.Content.ReadFromJsonAsync<AcceptedPayload>())!.SharePractice);
        await _base.WithDbAsync(async db => Assert.Single(await db.CoachInvitations.ToListAsync()));
    }

    [Fact]
    public async Task Invitations_ToYourself_OrToYourCoach_AreRefused()
    {
        var self = await InviteAsync("GOLFER@example.test");
        await InviteAsync("coach@example.test");
        await Client(_coachId).PostAsJsonAsync("/coach-invitations/accept", new { token = TokenFrom(_outbox.Sent.Last()) });
        var coachAgain = await InviteAsync("coach@example.test");

        await AssertFieldErrorAsync(self, "You can't invite yourself as your coach.");
        await AssertFieldErrorAsync(coachAgain, "This person is already your coach.");
    }

    [Fact]
    public async Task AGolfer_HasAtMostThreeCoaches_CountingOpenInvitations()
    {
        for (var i = 1; i <= 3; i++)
            Assert.Equal(HttpStatusCode.OK, (await InviteAsync($"coach{i}@example.test")).StatusCode);

        var fourth = await InviteAsync("coach4@example.test");
        var resend = await InviteAsync("coach1@example.test");

        await AssertFieldErrorAsync(fourth, "You can have at most 3 coaches, including invitations not yet accepted.");
        Assert.True((await resend.Content.ReadFromJsonAsync<InvitationPayload>())!.Resent);
    }

    [Fact]
    public async Task AGolfer_SendsAtMostTenInvitationEmailsADay()
    {
        for (var i = 0; i < 10; i++)
            (await InviteAsync("coach@example.test")).EnsureSuccessStatusCode();

        var eleventh = await InviteAsync("coach@example.test");

        Assert.Equal(HttpStatusCode.TooManyRequests, eleventh.StatusCode);
        Assert.Equal(10, _outbox.Sent.Count);
    }

    [Fact]
    public async Task DeletingAnAccount_RemovesItsInvitationsAndLinks_FromEitherSide()
    {
        await InviteAsync("coach@example.test");
        await Client(_coachId).PostAsJsonAsync("/coach-invitations/accept", new { token = TokenFrom(_outbox.Sent.Single()) });
        await InviteAsync("stranger@example.test");

        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AccountDeletionService>().DeleteAsync(_coachId, default);

        await _base.WithDbAsync(async db =>
        {
            Assert.Empty(await db.CoachLinks.ToListAsync());
            // The accepted invitation held the coach's address; the stranger's stays.
            Assert.Equal("stranger@example.test", (await db.CoachInvitations.SingleAsync()).Email);
        });

        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AccountDeletionService>().DeleteAsync(_golferId, default);

        await _base.WithDbAsync(async db => Assert.Empty(await db.CoachInvitations.ToListAsync()));
    }

    [Fact]
    public async Task TheInvitedCoach_SeesWhoInvitesAndWhatTheyShare_BeforeDeciding()
    {
        await InviteAsync("coach@example.test", sharePractice: false);
        var token = TokenFrom(_outbox.Sent.Single());

        var preview = await Client(_coachId).PostAsJsonAsync("/coach-invitations/preview", new { token });

        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var offered = (await preview.Content.ReadFromJsonAsync<PreviewPayload>())!;
        Assert.Equal("Sam", offered.GolferDisplayName);
        Assert.True(offered.ShareReflections);
        Assert.False(offered.SharePractice);

        // Looking at it doesn't use it up.
        Assert.Equal(HttpStatusCode.OK,
            (await Client(_coachId).PostAsJsonAsync("/coach-invitations/accept", new { token })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await Client(_coachId).PostAsJsonAsync("/coach-invitations/preview", new { token })).StatusCode);
    }

    [Fact]
    public async Task AForwardedLink_DoesntShowTheGolfersName()
    {
        await InviteAsync("coach@example.test");
        var token = TokenFrom(_outbox.Sent.Single());

        var preview = await Client(_strangerId).PostAsJsonAsync("/coach-invitations/preview", new { token });

        Assert.Equal(HttpStatusCode.Forbidden, preview.StatusCode);
        Assert.DoesNotContain("Sam", await preview.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound,
            (await Client(_coachId).PostAsJsonAsync("/coach-invitations/preview", new { token = "unknown" })).StatusCode);
    }

    [Fact]
    public async Task TheGolfer_SeesTheirCoachesAndOpenInvitations()
    {
        await InviteAsync("coach@example.test", shareReflections: false);
        await Client(_coachId).PostAsJsonAsync("/coach-invitations/accept", new { token = TokenFrom(_outbox.Sent.Single()) });
        await InviteAsync("pending@example.test", sharePractice: false);
        var cancelled = (await (await InviteAsync("cancelled@example.test")).Content.ReadFromJsonAsync<InvitationPayload>())!;
        await Client(_golferId).DeleteAsync($"/me/coach-invitations/{cancelled.CoachInvitationId}");
        var expired = (await (await InviteAsync("expired@example.test")).Content.ReadFromJsonAsync<InvitationPayload>())!;
        await _base.WithDbAsync(async db =>
        {
            var stored = await db.CoachInvitations.SingleAsync(i => i.CoachInvitationId == expired.CoachInvitationId);
            stored.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        });

        var list = (await Client(_golferId).GetFromJsonAsync<CoachesPayload>("/me/coaches"))!;

        var coach = Assert.Single(list.Coaches);
        Assert.False(coach.ShareReflections);
        Assert.True(coach.SharePractice);
        var invitation = Assert.Single(list.Invitations);
        Assert.Equal("pending@example.test", invitation.Email);
        Assert.False(invitation.SharePractice);

        // Only the caller's own: being someone's coach doesn't make them your coach.
        var theirs = (await Client(_coachId).GetFromJsonAsync<CoachesPayload>("/me/coaches"))!;
        Assert.Empty(theirs.Coaches);
        Assert.Empty(theirs.Invitations);
    }

    [Fact]
    public async Task EveryEndpoint_NeedsASignedInUser()
    {
        var anonymous = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsJsonAsync("/me/coach-invitations", new { email = "coach@example.test" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.DeleteAsync($"/me/coach-invitations/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/me/coaches")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsJsonAsync("/coach-invitations/preview", new { token = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsJsonAsync("/coach-invitations/accept", new { token = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsJsonAsync("/coach-invitations/decline", new { token = "x" })).StatusCode);
    }

    private Task<HttpResponseMessage> InviteAsync(string email, bool? shareReflections = null, bool? sharePractice = null) =>
        Client(_golferId).PostAsJsonAsync("/me/coach-invitations", new { email, shareReflections, sharePractice });

    private HttpClient Client(Guid userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWorld.TokenFor(userId));
        return client;
    }

    private static async Task AssertFieldErrorAsync(HttpResponseMessage response, string message)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>();
        Assert.Equal(message, Assert.Single(problem!.Errors["Email"]));
    }

    private static string TokenFrom(EmailMessage email) =>
        Uri.UnescapeDataString(Regex.Match(email.PlainText, @"token=([^\s]+)").Groups[1].Value);

    private sealed class RecordingOutbox : IEmailOutbox
    {
        private readonly ConcurrentQueue<EmailMessage> _sent = new();

        public IReadOnlyCollection<EmailMessage> Sent => _sent.ToArray();

        public void Enqueue(EmailMessage message) => _sent.Enqueue(message);
    }

    private sealed record InvitationPayload(Guid CoachInvitationId, string Email, bool ShareReflections, bool SharePractice, bool Resent);

    private sealed record PreviewPayload(string GolferDisplayName, bool ShareReflections, bool SharePractice);

    private sealed record CoachPayload(Guid CoachLinkId, string DisplayName, bool ShareReflections, bool SharePractice);

    private sealed record CoachesPayload(IReadOnlyList<CoachPayload> Coaches, IReadOnlyList<InvitationPayload> Invitations);

    private sealed record AcceptedPayload(Guid CoachLinkId, Guid GolferId, string GolferDisplayName, bool ShareReflections, bool SharePractice);
}
