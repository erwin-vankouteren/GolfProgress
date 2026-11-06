using GolfProgress.Domain.Entities;
using System.Net;
using System.Net.Http.Json;

namespace GolfProgress.Api.Tests;

/// <summary>Issue #199: a coach's students, and `hasStudents` on GET /me.</summary>
public sealed class CoachStudentsTests : IAsyncLifetime
{
    private readonly ApiFactory _factory = new();
    private readonly Guid _coachId = Guid.NewGuid();
    private readonly Guid _otherCoachId = Guid.NewGuid();
    private readonly Guid _annId = Guid.NewGuid();
    private readonly Guid _benId = Guid.NewGuid();
    private readonly Guid _caraId = Guid.NewGuid();

    public async Task InitializeAsync() => await _factory.WithDbAsync(async db =>
    {
        db.Users.Add(TestWorld.NewUser(_coachId, "coach@example.test"));
        db.Users.Add(TestWorld.NewUser(_otherCoachId, "other@example.test"));
        foreach (var (id, name) in new[] { (_annId, "Ann"), (_benId, "Ben"), (_caraId, "Cara") })
        {
            var golfer = TestWorld.NewUser(id, $"{name}@example.test");
            golfer.DisplayName = name;
            db.Users.Add(golfer);
        }
        await db.SaveChangesAsync();
    });

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ACoach_SeesTheirStudents_NewestActivityFirst_WithoutTheirEmail()
    {
        await LinkAsync(_annId, _coachId);
        await LinkAsync(_benId, _coachId, shareReflections: false);
        await LinkAsync(_caraId, _coachId);
        await RoundAsync(_annId, new DateOnly(2026, 10, 1));
        await RoundAsync(_benId, new DateOnly(2026, 10, 20));
        await PracticeAsync(_annId, new DateOnly(2026, 10, 5));

        var response = await TestWorld.ClientFor(_factory, _coachId).GetAsync("/coach/students");

        Assert.Equal("3", response.Headers.GetValues("X-Total-Count").Single());
        Assert.DoesNotContain("@example.test", await response.Content.ReadAsStringAsync());
        var students = (await response.Content.ReadFromJsonAsync<List<StudentPayload>>())!;
        Assert.Equal(new[] { "Ben", "Ann", "Cara" }, students.Select(s => s.DisplayName));
        Assert.False(students[0].ShareReflections);
        Assert.Equal(new DateOnly(2026, 10, 5), students[1].LastActivityOn);
        Assert.Null(students[2].LastActivityOn);
        Assert.All(students, s => Assert.Equal(0, s.UnreadReplies));
    }

    [Fact]
    public async Task PracticeThatIsntShared_DoesntShowInLastActivity()
    {
        await LinkAsync(_annId, _coachId, sharePractice: false);
        await RoundAsync(_annId, new DateOnly(2026, 10, 1));
        await PracticeAsync(_annId, new DateOnly(2026, 10, 5));

        var student = Assert.Single(await StudentsOfAsync(_coachId));

        Assert.Equal(new DateOnly(2026, 10, 1), student.LastActivityOn);
    }

    [Fact]
    public async Task Students_ArePaged()
    {
        await LinkAsync(_annId, _coachId);
        await LinkAsync(_benId, _coachId);
        await LinkAsync(_caraId, _coachId);

        var response = await TestWorld.ClientFor(_factory, _coachId).GetAsync("/coach/students?skip=1&take=1");

        Assert.Equal("3", response.Headers.GetValues("X-Total-Count").Single());
        var student = Assert.Single((await response.Content.ReadFromJsonAsync<List<StudentPayload>>())!);
        Assert.Equal("Ben", student.DisplayName);
    }

    [Fact]
    public async Task AnEndedLink_AndAnotherCoachsStudent_ArentListed()
    {
        await LinkAsync(_annId, _coachId, endedAt: DateTimeOffset.UtcNow.AddMinutes(-1));
        await LinkAsync(_benId, _otherCoachId);

        Assert.Empty(await StudentsOfAsync(_coachId));
        Assert.False(await HasStudentsAsync(_coachId));
    }

    [Fact]
    public async Task HasStudents_IsTrue_OnlyForTheCoach_AlsoAfterChangingTheProfile()
    {
        await LinkAsync(_annId, _coachId);

        Assert.True(await HasStudentsAsync(_coachId));
        Assert.False(await HasStudentsAsync(_annId));

        var patched = await TestWorld.ClientFor(_factory, _coachId).PatchAsJsonAsync("/me", new { displayName = "Mark" });
        Assert.True((await patched.Content.ReadFromJsonAsync<MePayload>())!.HasStudents);
    }

    [Fact]
    public async Task TheList_NeedsASignedInUser() =>
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("/coach/students")).StatusCode);

    private Task LinkAsync(
        Guid golferId, Guid coachId, bool shareReflections = true, bool sharePractice = true, DateTimeOffset? endedAt = null) =>
        _factory.WithDbAsync(async db =>
        {
            db.CoachLinks.Add(new CoachLink
            {
                CoachLinkId = Guid.NewGuid(),
                GolferId = golferId,
                CoachId = coachId,
                ShareReflections = shareReflections,
                SharePractice = sharePractice,
                CreatedAt = DateTimeOffset.UtcNow,
                EndedAt = endedAt,
            });
            await db.SaveChangesAsync();
        });

    private Task RoundAsync(Guid userId, DateOnly playedAt) => _factory.WithDbAsync(async db =>
    {
        var round = TestWorld.NewRound(Guid.NewGuid(), userId);
        round.PlayedAt = playedAt;
        db.Rounds.Add(round);
        await db.SaveChangesAsync();
    });

    private Task PracticeAsync(Guid userId, DateOnly practicedAt) => _factory.WithDbAsync(async db =>
    {
        db.PracticeSessions.Add(new PracticeSession
        {
            PracticeSessionId = Guid.NewGuid(),
            UserId = userId,
            PracticedAt = practicedAt,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    });

    private async Task<List<StudentPayload>> StudentsOfAsync(Guid coachId) =>
        (await TestWorld.ClientFor(_factory, coachId).GetFromJsonAsync<List<StudentPayload>>("/coach/students"))!;

    private async Task<bool> HasStudentsAsync(Guid userId) =>
        (await TestWorld.ClientFor(_factory, userId).GetFromJsonAsync<MePayload>("/me"))!.HasStudents;

    private sealed record StudentPayload(
        Guid GolferId, string DisplayName, DateOnly? LastActivityOn, int UnreadReplies, bool ShareReflections, bool SharePractice);

    private sealed record MePayload(bool HasStudents);
}
