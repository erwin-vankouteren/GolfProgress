using System.Net.Http.Json;

namespace GolfProgress.Api.Tests;

/// <summary>Regression guard for issue #13: GET /me/rounds is paged.</summary>
public sealed class RoundPagingTests : IAsyncLifetime
{
    private const int RoundCount = 25;

    private ApiFactory _factory = null!;
    private Guid _userId;

    public async Task InitializeAsync()
    {
        _factory = new ApiFactory();
        _userId = Guid.NewGuid();

        await _factory.WithDbAsync(async db =>
        {
            db.Users.Add(TestWorld.NewUser(_userId, "paging@example.test"));

            // Two rounds per day, so same-day ordering is exercised too.
            for (var i = 0; i < RoundCount; i++)
            {
                var round = TestWorld.NewRound(Guid.NewGuid(), _userId);
                round.PlayedAt = new DateOnly(2026, 5, 1).AddDays(i / 2);
                round.CreatedAt = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero).AddHours(i);
                db.Rounds.Add(round);
            }

            await db.SaveChangesAsync();
        });
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task DefaultPage_Is20NewestFirst_WithTheTotalInAHeader()
    {
        var response = await Client().GetAsync("/me/rounds");
        var rounds = await response.Content.ReadFromJsonAsync<List<RoundPayload>>();

        Assert.Equal(20, rounds!.Count);
        Assert.Equal(RoundCount.ToString(), response.Headers.GetValues("X-Total-Count").Single());
        Assert.Equal(rounds.OrderByDescending(r => r.PlayedAt).Select(r => r.RoundId), rounds.Select(r => r.RoundId));
    }

    [Fact]
    public async Task Pages_CoverEveryRoundExactlyOnce()
    {
        var seen = new List<Guid>();
        for (var skip = 0; skip < RoundCount; skip += 10)
        {
            var page = await Client().GetFromJsonAsync<List<RoundPayload>>($"/me/rounds?skip={skip}&take=10");
            seen.AddRange(page!.Select(r => r.RoundId));
        }

        Assert.Equal(RoundCount, seen.Count);
        Assert.Equal(RoundCount, seen.Distinct().Count());
    }

    [Theory]
    // take, expected page size
    [InlineData(0, 1)]       // clamped up to 1
    [InlineData(-5, 1)]
    [InlineData(1000, 25)]   // clamped to 100; only 25 exist
    public async Task Take_IsClamped(int take, int expected)
    {
        var rounds = await Client().GetFromJsonAsync<List<RoundPayload>>($"/me/rounds?take={take}");

        Assert.Equal(expected, rounds!.Count);
    }

    [Fact]
    public async Task NegativeSkip_IsTreatedAsZero_AndSkipPastTheEndIsEmpty()
    {
        var first = await Client().GetFromJsonAsync<List<RoundPayload>>("/me/rounds?take=5");
        var negative = await Client().GetFromJsonAsync<List<RoundPayload>>("/me/rounds?skip=-3&take=5");
        var beyond = await Client().GetFromJsonAsync<List<RoundPayload>>($"/me/rounds?skip={RoundCount}");

        Assert.Equal(first!.Select(r => r.RoundId), negative!.Select(r => r.RoundId));
        Assert.Empty(beyond!);
    }

    private HttpClient Client() => TestWorld.ClientFor(_factory, _userId);

    private sealed record RoundPayload(Guid RoundId, string PlayedAt);
}
