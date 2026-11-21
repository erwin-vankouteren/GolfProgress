using GolfProgress.Api.Services;
using GolfProgress.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace GolfProgress.Api.Tests;

/// <summary>
/// Issue #212: feedback threads on a golfer's rounds, reflections and practice sessions.
/// Each coach has their own thread with the golfer; either side can write first.
/// </summary>
public sealed class FeedbackTests : IAsyncLifetime
{
    private readonly ApiFactory _factory = new();
    private readonly Guid _coachId = Guid.NewGuid();
    private readonly Guid _otherCoachId = Guid.NewGuid();
    private readonly Guid _annId = Guid.NewGuid();
    private readonly Guid _benId = Guid.NewGuid();

    public async Task InitializeAsync() => await _factory.WithDbAsync(async db =>
    {
        foreach (var (id, name) in new[] { (_coachId, "Pro"), (_otherCoachId, "Other"), (_annId, "Ann"), (_benId, "Ben") })
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
    public async Task ACoachWrites_TheGolferReplies_AndUnreadRepliesFollow()
    {
        await LinkAsync(_coachId);
        var roundId = await RoundAsync(_annId);

        var written = await Coach().PostAsJsonAsync("/feedback", Message(roundId, "  Great tempo on the back nine.  "));
        Assert.Equal(HttpStatusCode.OK, written.StatusCode);
        var first = (await written.Content.ReadFromJsonAsync<MessagePayload>())!;
        Assert.Equal("Great tempo on the back nine.", first.Text);
        Assert.True(first.FromCoach);
        Assert.True(first.Mine);

        var thread = await ThreadAsync(Golfer(), roundId);
        var seen = Assert.Single(thread);
        Assert.Equal("Pro", seen.CoachName);
        Assert.False(seen.Mine);

        (await Golfer().PostAsJsonAsync("/feedback", Message(roundId, "Thanks! What about putting?", coachId: _coachId)))
            .EnsureSuccessStatusCode();

        var coachView = await ThreadAsync(Coach(), roundId);
        Assert.Equal(new[] { true, false }, coachView.Select(m => m.FromCoach));
        Assert.Equal(1, await UnreadRepliesAsync());

        var read = await Coach().PostAsJsonAsync("/feedback/read", Read(roundId));
        Assert.Equal(HttpStatusCode.NoContent, read.StatusCode);
        Assert.Equal(0, await UnreadRepliesAsync());

        await _factory.WithDbAsync(async db =>
            Assert.Equal(2, await db.Events.CountAsync(e => e.EventType == "FeedbackWritten")));
    }

    [Fact]
    public async Task TheGolfer_CanStartAThread_WithACoachWhoCanSeeTheItem()
    {
        await LinkAsync(_coachId, shareReflections: false);
        var roundId = await RoundAsync(_annId);
        var reflectionId = await ReflectAsync(roundId);

        Assert.Equal(HttpStatusCode.OK,
            (await Golfer().PostAsJsonAsync("/feedback", Message(roundId, "Can we look at this one?", coachId: _coachId))).StatusCode);
        Assert.Single(await ThreadAsync(Coach(), roundId));

        // No coach named, a coach who can't see reflections, or someone who isn't their coach.
        foreach (var request in new object[]
                 {
                     Message(roundId, "Hello?"),
                     Message(reflectionId, "On my reflection", type: "Reflection", coachId: _coachId),
                     Message(roundId, "Hello?", coachId: _otherCoachId),
                 })
        {
            var response = await Golfer().PostAsJsonAsync("/feedback", request);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("coachId", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task EachCoach_SeesOnlyTheirOwnThread_AndTheGolferSeesBoth()
    {
        await LinkAsync(_coachId);
        await LinkAsync(_otherCoachId);
        var roundId = await RoundAsync(_annId);

        await Coach().PostAsJsonAsync("/feedback", Message(roundId, "From Pro"));
        await TestWorld.ClientFor(_factory, _otherCoachId).PostAsJsonAsync("/feedback", Message(roundId, "From Other"));

        Assert.Equal(new[] { "From Pro" }, (await ThreadAsync(Coach(), roundId)).Select(m => m.Text));
        var otherView = await TestWorld.ClientFor(_factory, _otherCoachId).GetAsync(ThreadUrl(roundId));
        Assert.DoesNotContain("Pro", await otherView.Content.ReadAsStringAsync());
        Assert.Equal(new[] { "Pro", "Other" }, (await ThreadAsync(Golfer(), roundId)).Select(m => m.CoachName));
    }

    [Fact]
    public async Task Text_IsOneTo2000Characters_AndTheTargetTypeMustBeKnown()
    {
        await LinkAsync(_coachId);
        var roundId = await RoundAsync(_annId);

        foreach (var text in new[] { "", "   ", new string('a', 2001) })
            Assert.Equal(HttpStatusCode.BadRequest, (await Coach().PostAsJsonAsync("/feedback", Message(roundId, text))).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await Coach().PostAsJsonAsync("/feedback", Message(roundId, new string('a', 2000)))).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await Coach().PostAsJsonAsync("/feedback", Message(roundId, "Hi", type: "Lesson"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await Coach().GetAsync($"/feedback?golferId={_annId}&targetType=Lesson&targetId={roundId}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Coach().GetAsync($"/feedback?golferId={_annId}")).StatusCode);
    }

    [Fact]
    public async Task WithoutAccess_ReadingAndWritingAre404_AndRevealNothing()
    {
        var roundId = await RoundAsync(_annId);
        var sessionId = await PracticeAsync(_annId);
        var reflectionId = await ReflectAsync(roundId);
        var bensRound = await RoundAsync(_benId);
        await LinkAsync(_coachId, shareReflections: false, sharePractice: false);
        await LinkAsync(_otherCoachId, endedAt: DateTimeOffset.UtcNow.AddMinutes(-1));
        await Golfer().PostAsJsonAsync("/feedback", Message(roundId, "Secret", coachId: _coachId));

        var cases = new (HttpClient Client, Guid Target, string Type)[]
        {
            (Coach(), reflectionId, "Reflection"),        // reflections not shared
            (Coach(), sessionId, "PracticeSession"),      // practice not shared
            (Coach(), bensRound, "Round"),                // another golfer's round, under Ann
            (Coach(), Guid.NewGuid(), "Round"),           // no such round
            (TestWorld.ClientFor(_factory, _otherCoachId), roundId, "Round"), // ended link
            (TestWorld.ClientFor(_factory, _benId), roundId, "Round"),        // a stranger
        };
        foreach (var (client, target, type) in cases)
        {
            var read = await client.GetAsync(ThreadUrl(target, type));
            Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
            Assert.DoesNotContain("Secret", await read.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.NotFound,
                (await client.PostAsJsonAsync("/feedback", Message(target, "Hi", type: type))).StatusCode);
        }

        Assert.Equal(HttpStatusCode.NotFound,
            (await TestWorld.ClientFor(_factory, _benId).PostAsJsonAsync("/feedback/read", Read(roundId))).StatusCode);
        // Nor can a coach mark read what they can't see.
        Assert.Equal(HttpStatusCode.NotFound,
            (await Coach().PostAsJsonAsync("/feedback/read", Read(reflectionId, "Reflection"))).StatusCode);
    }

    [Fact]
    public async Task AuthorsEditAndDelete_OnlyTheirOwnMessages_WhileTheyCanSeeThem()
    {
        var linkId = await LinkAsync(_coachId);
        var roundId = await RoundAsync(_annId);
        var coachMessage = await WriteAsync(Coach(), Message(roundId, "First"));
        var golferMessage = await WriteAsync(Golfer(), Message(roundId, "Reply", coachId: _coachId));

        var edited = await Coach().PatchAsJsonAsync($"/feedback/{coachMessage}", new { text = "First, edited" });
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        Assert.NotNull((await edited.Content.ReadFromJsonAsync<MessagePayload>())!.EditedAt);

        Assert.Equal(HttpStatusCode.NotFound, (await Coach().PatchAsJsonAsync($"/feedback/{golferMessage}", new { text = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Coach().DeleteAsync($"/feedback/{golferMessage}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Golfer().DeleteAsync($"/feedback/{coachMessage}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Coach().PatchAsJsonAsync($"/feedback/{coachMessage}", new { text = " " })).StatusCode);

        // Removed, the coach can't reach what they wrote; the golfer still has it.
        await Golfer().DeleteAsync($"/me/coaches/{linkId}");
        Assert.Equal(HttpStatusCode.NotFound, (await Coach().DeleteAsync($"/feedback/{coachMessage}")).StatusCode);
        Assert.Equal(2, (await ThreadAsync(Golfer(), roundId)).Count);

        Assert.Equal(HttpStatusCode.NoContent, (await Golfer().DeleteAsync($"/feedback/{golferMessage}")).StatusCode);
        Assert.Equal("First, edited", Assert.Single(await ThreadAsync(Golfer(), roundId)).Text);
    }

    [Fact]
    public async Task AtMost60MessagesAnHour()
    {
        await LinkAsync(_coachId);
        var roundId = await RoundAsync(_annId);
        await _factory.WithDbAsync(async db =>
        {
            for (var i = 0; i < 60; i++)
                db.Events.Add(new EventLog
                {
                    EventLogId = Guid.NewGuid(), UserId = _coachId, EventType = "FeedbackWritten", RelatedId = Guid.NewGuid(),
                    PayloadJson = JsonDocument.Parse("{}"), CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-30),
                });
            await db.SaveChangesAsync();
        });

        Assert.Equal(HttpStatusCode.TooManyRequests, (await Coach().PostAsJsonAsync("/feedback", Message(roundId, "One more"))).StatusCode);
        // The golfer has their own allowance.
        Assert.Equal(HttpStatusCode.OK,
            (await Golfer().PostAsJsonAsync("/feedback", Message(roundId, "Mine", coachId: _coachId))).StatusCode);
    }

    [Fact]
    public async Task DeletingARoundOrSession_DeletesItsThreads()
    {
        await LinkAsync(_coachId);
        var roundId = await RoundAsync(_annId);
        var reflectionId = await ReflectAsync(roundId);
        var sessionId = await PracticeAsync(_annId);
        var keptRound = await RoundAsync(_annId);
        await WriteAsync(Coach(), Message(roundId, "On the round"));
        await WriteAsync(Coach(), Message(reflectionId, "On the reflection", type: "Reflection"));
        await WriteAsync(Coach(), Message(sessionId, "On the session", type: "PracticeSession"));
        await WriteAsync(Coach(), Message(keptRound, "On another round"));

        (await Golfer().DeleteAsync($"/rounds/{roundId}")).EnsureSuccessStatusCode();
        (await Golfer().DeleteAsync($"/me/practice-sessions/{sessionId}")).EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
            Assert.Equal("On another round", (await db.FeedbackMessages.SingleAsync()).Text));
    }

    [Fact]
    public async Task ADeletedCoachsMessages_StayWithTheGolfer_FromAFormerCoach()
    {
        await LinkAsync(_coachId);
        var roundId = await RoundAsync(_annId);
        await WriteAsync(Coach(), Message(roundId, "Keep this"));

        await _factory.WithDbAsync(async db =>
            Assert.True(await new AccountDeletionService(db).DeleteAsync(_coachId, CancellationToken.None)));

        var message = Assert.Single(await ThreadAsync(Golfer(), roundId));
        Assert.Equal("Keep this", message.Text);
        Assert.Null(message.CoachId);
        Assert.Null(message.CoachName);
        Assert.True(message.FromCoach);
    }

    [Fact]
    public async Task ADeletedGolfersThreads_LeaveNothingAboutThemInTheCoachsLog()
    {
        await LinkAsync(_coachId);
        var annsRound = await RoundAsync(_annId);
        await WriteAsync(Coach(), Message(annsRound, "About Ann's round"));
        // Ben's thread with the same coach, which stays.
        await _factory.WithDbAsync(async db =>
        {
            db.CoachLinks.Add(new CoachLink
            {
                CoachLinkId = Guid.NewGuid(), GolferId = _benId, CoachId = _coachId,
                ShareReflections = true, SharePractice = true, CreatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        });
        var bensRound = await RoundAsync(_benId);
        await WriteAsync(Coach(), new { golferId = _benId, targetType = "Round", targetId = bensRound, text = "About Ben's round" });

        await _factory.WithDbAsync(async db =>
            Assert.True(await new AccountDeletionService(db).DeleteAsync(_annId, CancellationToken.None)));

        await _factory.WithDbAsync(async db =>
        {
            var left = await db.Events.Where(e => e.UserId == _coachId && e.EventType == "FeedbackWritten").ToListAsync();
            var only = Assert.Single(left);
            Assert.DoesNotContain(_annId.ToString(), only.PayloadJson!.RootElement.GetRawText());
            Assert.Contains(_benId.ToString(), only.PayloadJson!.RootElement.GetRawText());
        });
    }

    [Fact]
    public async Task TheGolfer_SeesWhatsNewFromTheirCoaches_WithWhatEachIsAbout()
    {
        await LinkAsync(_coachId);
        var roundId = await RoundAsync(_annId);
        var reflectionId = await ReflectAsync(roundId);
        var sessionId = await PracticeAsync(_annId);
        Assert.Equal(0, (await UnreadAsync()).Count);

        await WriteAsync(Coach(), Message(roundId, "On the round"));
        await WriteAsync(Coach(), Message(roundId, "And another thing"));
        await WriteAsync(Coach(), Message(reflectionId, "On your reflection", type: "Reflection"));
        await WriteAsync(Coach(), Message(sessionId, new string('p', 300), type: "PracticeSession"));
        // The golfer's own messages aren't news to them.
        await WriteAsync(Golfer(), Message(roundId, "My question", coachId: _coachId));

        var unread = await UnreadAsync();
        Assert.Equal(4, unread.Count);
        Assert.Equal(new[] { "PracticeSession", "Reflection", "Round" }, unread.Threads.Select(t => t.TargetType));
        var session = unread.Threads[0];
        Assert.Null(session.RoundId);
        Assert.Equal("2026-10-10", session.OnDate);
        Assert.Equal(140, session.Preview.Length);
        Assert.Equal(roundId, unread.Threads[1].RoundId);
        Assert.Equal("2026-10-15", unread.Threads[1].OnDate);
        var round = unread.Threads[2];
        Assert.Equal(2, round.Unread);
        Assert.Equal("Pro", round.CoachName);
        Assert.Equal("And another thing", round.Preview);
    }

    [Fact]
    public async Task OpeningOneThread_LeavesTheOthersUnread()
    {
        await LinkAsync(_coachId);
        var first = await RoundAsync(_annId);
        var second = await RoundAsync(_annId);
        await WriteAsync(Coach(), Message(first, "On the first"));
        await WriteAsync(Coach(), Message(second, "On the second, newer"));

        // Read up to the newest message, but only on the second round.
        Assert.Equal(HttpStatusCode.NoContent, (await Golfer().PostAsJsonAsync("/feedback/read", Read(second))).StatusCode);

        var unread = await UnreadAsync();
        Assert.Equal(1, unread.Count);
        Assert.Equal(first, Assert.Single(unread.Threads).TargetId);

        // Likewise for the coach's unread replies.
        await WriteAsync(Golfer(), Message(first, "Reply one", coachId: _coachId));
        await WriteAsync(Golfer(), Message(second, "Reply two", coachId: _coachId));
        await Coach().PostAsJsonAsync("/feedback/read", Read(second));
        Assert.Equal(1, await UnreadRepliesAsync());
    }

    [Fact]
    public async Task MarkingRead_NeedsAKnownTargetType()
    {
        Assert.Equal(HttpStatusCode.BadRequest,
            (await Golfer().PostAsJsonAsync("/feedback/read", Read(Guid.NewGuid(), "Lesson"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await Golfer().PostAsJsonAsync("/feedback/read", new { golferId = _annId })).StatusCode);
    }

    [Fact]
    public async Task EveryEndpoint_NeedsASignedInUser()
    {
        var anonymous = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(ThreadUrl(Guid.NewGuid()))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/feedback", Message(Guid.NewGuid(), "x"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PatchAsJsonAsync($"/feedback/{Guid.NewGuid()}", new { text = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.DeleteAsync($"/feedback/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/feedback/read", Read(Guid.NewGuid()))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/me/feedback/unread")).StatusCode);
    }

    private HttpClient Coach() => TestWorld.ClientFor(_factory, _coachId);
    private HttpClient Golfer() => TestWorld.ClientFor(_factory, _annId);

    private object Message(Guid targetId, string text, string type = "Round", Guid? coachId = null) =>
        new { golferId = _annId, targetType = type, targetId, coachId, text };

    private object Read(Guid targetId, string type = "Round") =>
        new { golferId = _annId, targetType = type, targetId };

    private async Task<UnreadPayload> UnreadAsync() =>
        (await Golfer().GetFromJsonAsync<UnreadPayload>("/me/feedback/unread"))!;

    private string ThreadUrl(Guid targetId, string type = "Round") =>
        $"/feedback?golferId={_annId}&targetType={type}&targetId={targetId}";

    private async Task<List<MessagePayload>> ThreadAsync(HttpClient client, Guid roundId) =>
        (await client.GetFromJsonAsync<List<MessagePayload>>(ThreadUrl(roundId)))!;

    private static async Task<Guid> WriteAsync(HttpClient client, object message)
    {
        var response = await client.PostAsJsonAsync("/feedback", message);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MessagePayload>())!.FeedbackMessageId;
    }

    private async Task<int> UnreadRepliesAsync() =>
        Assert.Single((await Coach().GetFromJsonAsync<List<StudentPayload>>("/coach/students"))!).UnreadReplies;

    private async Task<Guid> RoundAsync(Guid userId)
    {
        var response = await TestWorld.ClientFor(_factory, userId).PostAsJsonAsync("/me/rounds",
            new { playedAt = "2026-10-15", holes = 18, totalStrokes = 88 });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdPayload>())!.RoundId;
    }

    private async Task<Guid> ReflectAsync(Guid roundId)
    {
        var response = await Golfer().PostAsJsonAsync($"/rounds/{roundId}/reflections", new { focusRating = 4 });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdPayload>())!.ReflectionId;
    }

    private async Task<Guid> PracticeAsync(Guid userId)
    {
        var response = await TestWorld.ClientFor(_factory, userId).PostAsJsonAsync("/me/practice-sessions",
            new { practicedAt = "2026-10-10", sessionType = "Putting", durationMinutes = 30 });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdPayload>())!.PracticeSessionId;
    }

    private async Task<Guid> LinkAsync(
        Guid coachId, bool shareReflections = true, bool sharePractice = true, DateTimeOffset? endedAt = null)
    {
        var linkId = Guid.NewGuid();
        await _factory.WithDbAsync(async db =>
        {
            db.CoachLinks.Add(new CoachLink
            {
                CoachLinkId = linkId,
                GolferId = _annId,
                CoachId = coachId,
                ShareReflections = shareReflections,
                SharePractice = sharePractice,
                CreatedAt = DateTimeOffset.UtcNow,
                EndedAt = endedAt,
            });
            await db.SaveChangesAsync();
        });
        return linkId;
    }

    private sealed record IdPayload(Guid RoundId, Guid ReflectionId, Guid PracticeSessionId);

    private sealed record MessagePayload(
        Guid FeedbackMessageId, Guid? CoachId, string? CoachName, bool FromCoach, bool Mine, string Text, DateTimeOffset? EditedAt);

    private sealed record StudentPayload(int UnreadReplies);

    private sealed record UnreadPayload(int Count, List<UnreadThreadPayload> Threads);

    private sealed record UnreadThreadPayload(
        string TargetType, Guid TargetId, Guid? RoundId, string? OnDate, Guid? CoachId, string? CoachName, int Unread, string Preview);
}
