using GolfProgress.Api.Configuration;
using GolfProgress.Api.Email;
using GolfProgress.Api.Services;
using GolfProgress.Domain.Entities;
using GolfProgress.Domain.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace GolfProgress.Api.Tests;

/// <summary>
/// #240 and #245: the daily feedback digest to golfers and the weekly coach digest, each sent
/// once, only with something to say, and not to whoever switched it off.
/// </summary>
public sealed class DigestTests : IAsyncLifetime
{
    // A Monday, after the 7:00 UTC send time: both digests are due.
    private static readonly DateTimeOffset Monday = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);

    private ApiFactory _base = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private readonly RecordingSender _sender = new();

    private readonly Guid _annId = Guid.NewGuid();
    private readonly Guid _benId = Guid.NewGuid();
    private readonly Guid _coachId = Guid.NewGuid();
    private readonly Guid _otherCoachId = Guid.NewGuid();
    private Guid _annLink;

    public async Task InitializeAsync()
    {
        _base = new ApiFactory();
        _factory = _base.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IEmailSender>();
            s.AddSingleton<IEmailSender>(_sender);
        }));

        await _base.WithDbAsync(async db =>
        {
            var ann = TestWorld.NewUser(_annId, "ann@example.test");
            ann.DisplayName = "Ann";
            ann.Language = AppLanguage.Dutch;
            var ben = TestWorld.NewUser(_benId, "ben@example.test");
            ben.DisplayName = "Ben";
            var coach = TestWorld.NewUser(_coachId, "coach@example.test");
            coach.DisplayName = "Pro";
            db.Users.AddRange(ann, ben, coach, TestWorld.NewUser(_otherCoachId, "other@example.test"));
            await db.SaveChangesAsync();
        });
        _annLink = await LinkAsync(_annId, _coachId, sharePractice: false);
        await LinkAsync(_benId, _coachId);
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        _base.Dispose();
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------ feedback digest

    [Fact]
    public async Task AGolferWithUnreadFeedback_GetsOneDigest_InTheirLanguage()
    {
        var roundId = await RoundAsync(_annId, Monday.AddDays(-3));
        await MessageAsync(_annId, _coachId, _coachId, FeedbackTargetType.Round, roundId, "Mooie ronde, let op je putts.", Monday.AddHours(-3));
        await MessageAsync(_annId, _coachId, _coachId, FeedbackTargetType.Round, roundId, "En je chips!", Monday.AddHours(-2));

        await RunAsync(Monday);

        var email = Assert.Single(FeedbackDigests());
        Assert.Equal("ann@example.test", email.To);
        Assert.Equal("Nieuwe feedback van je coach", email.Subject);
        Assert.Contains("Je hebt 2 ongelezen berichten van je coach.", email.PlainText);
        Assert.Contains("Pro over je ronde van 1 mei: „En je chips!” (en nog 1)", email.PlainText);
        Assert.Contains("http://localhost:5173/profile", email.PlainText);
        Assert.Contains("<li>", email.Html);

        // Later the same day, another instance, or the next day with nothing new: no more.
        await RunAsync(Monday.AddHours(3));
        await RunAsync(Monday.AddDays(1));
        Assert.Single(FeedbackDigests());
    }

    [Fact]
    public async Task NewFeedbackTheNextDay_IsInTheNextDigest_InEnglishWhenNoLanguageIsChosen()
    {
        var roundId = await RoundAsync(_benId, Monday.AddDays(-3));
        await RunAsync(Monday);
        await MessageAsync(_benId, _coachId, _coachId, FeedbackTargetType.Round, roundId, "Good round.", Monday.AddHours(5));

        await RunAsync(Monday.AddDays(1));

        var email = Assert.Single(FeedbackDigests());
        Assert.Equal("New feedback from your coach", email.Subject);
        Assert.Contains("You have 1 unread message from your coach.", email.PlainText);
        Assert.Contains("Pro on your round on 1 May: “Good round.”", email.PlainText);
    }

    [Fact]
    public async Task NoDigest_WhenItsRead_OnlyTheGolfersOwn_OrSwitchedOff()
    {
        var annRound = await RoundAsync(_annId, Monday.AddDays(-3));
        var benRound = await RoundAsync(_benId, Monday.AddDays(-3));
        // Ann read it; Ben only wrote himself.
        await MessageAsync(_annId, _coachId, _coachId, FeedbackTargetType.Round, annRound, "Nice.", Monday.AddHours(-3));
        await _base.WithDbAsync(async db =>
        {
            db.FeedbackReads.Add(new FeedbackRead
            {
                UserId = _annId, GolferId = _annId, TargetType = FeedbackTargetType.Round, TargetId = annRound,
                ReadUpTo = Monday.AddHours(-1),
            });
            await db.SaveChangesAsync();
        });
        await MessageAsync(_benId, _coachId, _benId, FeedbackTargetType.Round, benRound, "A question.", Monday.AddHours(-3));

        await RunAsync(Monday);
        Assert.Empty(FeedbackDigests());

        Assert.Equal(HttpStatusCode.OK, (await PatchMeAsync(_annId, new { emailFeedbackDigest = false })).StatusCode);
        await MessageAsync(_annId, _coachId, _coachId, FeedbackTargetType.Round, annRound, "Unread now.", Monday.AddHours(1));
        await RunAsync(Monday.AddDays(1));

        Assert.Empty(FeedbackDigests());
    }

    // ------------------------------------------------------------ coach digest

    [Fact]
    public async Task TheCoach_GetsEachActiveStudentsWeek_LimitedToWhatTheyShare()
    {
        await RoundAsync(_annId, Monday.AddDays(-2));
        await RoundAsync(_annId, Monday.AddDays(-1));
        await SessionAsync(_annId, Monday.AddDays(-1)); // Ann doesn't share practice.
        var benRound = await RoundAsync(_benId, Monday.AddDays(-20), createdAt: Monday.AddDays(-20));
        await SessionAsync(_benId, Monday.AddDays(-3));
        await MessageAsync(_benId, _coachId, _benId, FeedbackTargetType.Round, benRound, "Thanks!", Monday.AddDays(-2));
        // A reply in another coach's thread isn't this coach's.
        await MessageAsync(_benId, _otherCoachId, _benId, FeedbackTargetType.Round, benRound, "Hi", Monday.AddDays(-2));

        await RunAsync(Monday);

        var email = Assert.Single(CoachDigests());
        Assert.Equal("coach@example.test", email.To);
        Assert.Contains("- Ann: 2 new rounds\n", email.PlainText);
        Assert.Contains("- Ben: 1 practice session and 1 reply to your feedback\n", email.PlainText);
        Assert.Contains("http://localhost:5173/students", email.PlainText);

        // Once a week.
        await RunAsync(Monday.AddDays(2));
        Assert.Single(CoachDigests());
    }

    [Fact]
    public async Task NoCoachDigest_WhenNothingHappened_TheLinkEnded_OrSwitchedOff()
    {
        await RunAsync(Monday);
        Assert.Empty(CoachDigests());

        // An ended link: Ann's activity isn't the coach's business any more.
        await _base.WithDbAsync(async db =>
        {
            (await db.CoachLinks.FindAsync(_annLink))!.EndedAt = Monday;
            await db.SaveChangesAsync();
        });
        await RoundAsync(_annId, Monday.AddDays(2));
        await RunAsync(Monday.AddDays(7));
        Assert.Empty(CoachDigests());

        await RoundAsync(_benId, Monday.AddDays(9));
        Assert.Equal(HttpStatusCode.OK, (await PatchMeAsync(_coachId, new { emailCoachDigest = false })).StatusCode);
        await RunAsync(Monday.AddDays(14));
        Assert.Empty(CoachDigests());
    }

    [Fact]
    public async Task AStudentWithoutCoaching_IsLeftOutOfTheCoachDigest()
    {
        // Subscriptions started long ago (#312): Ann's trial is over and she has no Coaching;
        // Ben pays for the subscription with Coaching.
        var launched = _factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
            s.PostConfigure<PaymentsOptions>(o => o.LaunchDate = DateTimeOffset.UtcNow.AddDays(-90))));
        await _base.WithDbAsync(async db =>
        {
            foreach (var user in db.Users) user.CreatedAt = DateTimeOffset.UtcNow.AddDays(-120);
            foreach (var product in new[] { SubscriptionProduct.Premium, SubscriptionProduct.Coaching })
            {
                db.Subscriptions.Add(new Subscription
                {
                    SubscriptionId = Guid.NewGuid(), UserId = _benId, Source = SubscriptionSource.Stripe, ExternalId = "sub_ben",
                    Product = product, Period = SubscriptionPeriod.Month, Status = SubscriptionStatus.Active,
                    CurrentPeriodEnd = DateTimeOffset.UtcNow.AddDays(10), UpdatedAt = DateTimeOffset.UtcNow,
                });
            }
            await db.SaveChangesAsync();
        });
        await RoundAsync(_annId, Monday.AddDays(-1));
        await RoundAsync(_benId, Monday.AddDays(-1));

        using (var scope = launched.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<DigestService>().RunDueAsync(Monday, CancellationToken.None);

        var email = Assert.Single(CoachDigests());
        Assert.DoesNotContain("Ann", email.PlainText);
        Assert.Contains("- Ben: 1 new round", email.PlainText);
    }

    [Fact]
    public async Task ADigestAnotherInstanceClaimed_IsNotSentAgain()
    {
        var roundId = await RoundAsync(_annId, Monday.AddDays(-3));
        await MessageAsync(_annId, _coachId, _coachId, FeedbackTargetType.Round, roundId, "Nice.", Monday.AddHours(-3));
        await _base.WithDbAsync(async db =>
        {
            db.DigestRuns.Add(new DigestRun { Kind = DigestKind.Feedback, Period = new DateOnly(2026, 11, 16), StartedAt = Monday });
            await db.SaveChangesAsync();
        });

        await RunAsync(Monday);

        Assert.Empty(FeedbackDigests());
    }

    [Fact]
    public void TheDailyDigest_IsDueFromSevenUtc_AndTheCoachDigestOnMondays()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<DigestService>();

        Assert.Equal(new DateOnly(2026, 11, 15), service.DailySlot(Monday.AddHours(-2)));
        Assert.Equal(new DateOnly(2026, 11, 16), service.DailySlot(Monday));
        Assert.Equal(new DateOnly(2026, 11, 9), service.WeeklySlot(new DateOnly(2026, 11, 15)));
        Assert.Equal(new DateOnly(2026, 11, 16), service.WeeklySlot(new DateOnly(2026, 11, 21)));
    }

    // ------------------------------------------------------------ preferences

    [Fact]
    public async Task BothDigests_AreOnByDefault_AndSwitchOffWithPatchMe()
    {
        var me = await TestWorld.ClientFor(_base, _annId).GetFromJsonAsync<PreferencesPayload>("/me");
        Assert.Equal(new PreferencesPayload(true, true), me);

        var patched = await PatchMeAsync(_annId, new { emailFeedbackDigest = false });
        Assert.Equal(new PreferencesPayload(false, true), await patched.Content.ReadFromJsonAsync<PreferencesPayload>());

        var invalid = await PatchMeAsync(_annId, new { emailCoachDigest = (bool?)null });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    // ------------------------------------------------------------ helpers

    private IEnumerable<EmailMessage> FeedbackDigests() =>
        _sender.Sent.Where(m => m.Subject.Contains("feedback", StringComparison.OrdinalIgnoreCase));

    private IEnumerable<EmailMessage> CoachDigests() =>
        _sender.Sent.Where(m => m.Subject is "Your students this week" or "Je leerlingen deze week");

    private async Task RunAsync(DateTimeOffset now)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<DigestService>().RunDueAsync(now, CancellationToken.None);
    }

    private Task<HttpResponseMessage> PatchMeAsync(Guid userId, object body)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWorld.TokenFor(userId));
        return client.PatchAsJsonAsync("/me", body);
    }

    private async Task<Guid> LinkAsync(Guid golferId, Guid coachId, bool sharePractice = true)
    {
        var linkId = Guid.NewGuid();
        await _base.WithDbAsync(async db =>
        {
            db.CoachLinks.Add(new CoachLink
            {
                CoachLinkId = linkId, GolferId = golferId, CoachId = coachId,
                ShareReflections = true, SharePractice = sharePractice, CreatedAt = Monday.AddDays(-60),
            });
            await db.SaveChangesAsync();
        });
        return linkId;
    }

    private async Task<Guid> RoundAsync(Guid userId, DateTimeOffset loggedAt, DateTimeOffset? createdAt = null)
    {
        var round = TestWorld.NewRound(Guid.NewGuid(), userId);
        round.CreatedAt = createdAt ?? loggedAt;
        await _base.WithDbAsync(async db =>
        {
            db.Rounds.Add(round);
            await db.SaveChangesAsync();
        });
        return round.RoundId;
    }

    private Task SessionAsync(Guid userId, DateTimeOffset loggedAt) => _base.WithDbAsync(async db =>
    {
        db.PracticeSessions.Add(new PracticeSession
        {
            PracticeSessionId = Guid.NewGuid(), UserId = userId,
            PracticedAt = DateOnly.FromDateTime(loggedAt.UtcDateTime), CreatedAt = loggedAt,
        });
        await db.SaveChangesAsync();
    });

    private Task MessageAsync(
        Guid golferId, Guid coachId, Guid authorId, FeedbackTargetType type, Guid targetId, string text, DateTimeOffset at) =>
        _base.WithDbAsync(async db =>
        {
            db.FeedbackMessages.Add(new FeedbackMessage
            {
                FeedbackMessageId = Guid.NewGuid(), GolferId = golferId, CoachId = coachId, AuthorId = authorId,
                TargetType = type, TargetId = targetId, Text = text, CreatedAt = at,
            });
            await db.SaveChangesAsync();
        });

    private sealed class RecordingSender : IEmailSender
    {
        private readonly ConcurrentQueue<EmailMessage> _sent = new();

        public IReadOnlyCollection<EmailMessage> Sent => _sent.ToArray();

        public Task SendAsync(EmailMessage message, CancellationToken ct)
        {
            _sent.Enqueue(message);
            return Task.CompletedTask;
        }
    }

    private sealed record PreferencesPayload(bool EmailFeedbackDigest, bool EmailCoachDigest);
}
