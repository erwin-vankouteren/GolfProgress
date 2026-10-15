using GolfProgress.Api.Caching;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Http.Json;

namespace GolfProgress.Api.Tests;

/// <summary>
/// Regression guard for issue #14: cached dashboard and timeline responses are
/// evicted as soon as the user's data changes, instead of staying stale for up to
/// two minutes. Also pins the timeline's ?from/?to filter, which the cache used to
/// hide being ignored.
/// </summary>
public sealed class CacheInvalidationTests : IAsyncLifetime
{
    private ApiFactory _factory = null!;
    private Owner _owner = null!;
    private Owner _other = null!;

    public async Task InitializeAsync()
    {
        _factory = new ApiFactory();
        (_owner, _other) = await TestWorld.SeedTwoUsersAsync(_factory);
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Dashboard_AfterLoggingARound_ShowsTheNewRound()
    {
        var client = TestWorld.ClientFor(_factory, _owner.UserId);

        var before = await client.GetFromJsonAsync<DashboardPayload>("/me/dashboard");
        Assert.Equal(_owner.RoundId, before!.LastRound!.RoundId);

        var created = await LogRoundAsync(client, "2026-06-01");

        var after = await client.GetFromJsonAsync<DashboardPayload>("/me/dashboard");
        Assert.Equal(created, after!.LastRound!.RoundId);
    }

    [Fact]
    public async Task Dashboard_AfterCompute_ShowsTheNewScore()
    {
        var client = TestWorld.ClientFor(_factory, _owner.UserId);

        var before = await client.GetFromJsonAsync<DashboardPayload>("/me/dashboard");
        Assert.Null(before!.LastRound!.FitnessScore);

        await client.PostAsync($"/rounds/{_owner.RoundId}/compute", content: null);

        var after = await client.GetFromJsonAsync<DashboardPayload>("/me/dashboard");
        Assert.NotNull(after!.LastRound!.FitnessScore);
    }

    [Fact]
    public async Task Timeline_AfterLoggingARound_IncludesIt()
    {
        var client = TestWorld.ClientFor(_factory, _owner.UserId);

        var before = await client.GetFromJsonAsync<TimelinePayload>("/me/timeline");
        Assert.Equal(1, before!.Count);

        await LogRoundAsync(client, "2026-06-01");

        var after = await client.GetFromJsonAsync<TimelinePayload>("/me/timeline");
        Assert.Equal(2, after!.Count);
    }

    [Fact]
    public async Task OneUsersWrite_DoesNotEvictAnotherUsersCache()
    {
        var cache = (UserScopedCache)_factory.Services.GetService(typeof(UserScopedCache))!;
        var builds = 0;

        Task<int> Build() { builds++; return Task.FromResult(builds); }

        await cache.GetOrCreateAsync(_other.UserId, "probe", Build);

        await LogRoundAsync(TestWorld.ClientFor(_factory, _owner.UserId), "2026-06-01");

        await cache.GetOrCreateAsync(_other.UserId, "probe", Build);
        Assert.Equal(1, builds);
    }

    /// <summary>
    /// The timeline parsed ?from and ?to but called the service with nulls, and the
    /// cache key ignored them, so the filter never applied.
    /// </summary>
    [Fact]
    public async Task Timeline_FromAndTo_FilterAndAreCachedSeparately()
    {
        var client = TestWorld.ClientFor(_factory, _owner.UserId);
        await LogRoundAsync(client, "2026-06-01");   // seeded round is 2026-05-01

        var all = await client.GetFromJsonAsync<TimelinePayload>("/me/timeline");
        var june = await client.GetFromJsonAsync<TimelinePayload>("/me/timeline?from=2026-06-01");
        var may = await client.GetFromJsonAsync<TimelinePayload>("/me/timeline?to=2026-05-31");

        Assert.Equal(2, all!.Count);
        Assert.Equal(1, june!.Count);
        Assert.Equal(1, may!.Count);
    }

    private static async Task<Guid> LogRoundAsync(HttpClient client, string playedAt)
    {
        var response = await client.PostAsJsonAsync("/me/rounds",
            new { playedAt, holes = 18, totalStrokes = 88 });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RoundPayload>())!.RoundId;
    }

    private sealed record RoundPayload(Guid RoundId);
    private sealed record LastRoundPayload(Guid RoundId, short? FitnessScore);
    private sealed record DashboardPayload(LastRoundPayload? LastRound);
    private sealed record TimelinePayload(int Count);
}

/// <summary>Unit tests for <see cref="UserScopedCache"/> itself.</summary>
public sealed class UserScopedCacheTests
{
    private readonly UserScopedCache _cache = new(new MemoryCache(new MemoryCacheOptions()));
    private readonly Guid _user = Guid.NewGuid();

    [Fact]
    public async Task SecondRead_IsServedFromTheCache()
    {
        var builds = 0;
        Task<int> Build() => Task.FromResult(++builds);

        await _cache.GetOrCreateAsync(_user, "k", Build);
        var second = await _cache.GetOrCreateAsync(_user, "k", Build);

        Assert.Equal(1, second);
        Assert.Equal(1, builds);
    }

    [Fact]
    public async Task Invalidate_EvictsEveryKeyForThatUser()
    {
        var builds = 0;
        Task<int> Build() => Task.FromResult(++builds);

        await _cache.GetOrCreateAsync(_user, "a", Build);
        await _cache.GetOrCreateAsync(_user, "b", Build);

        _cache.Invalidate(_user);

        await _cache.GetOrCreateAsync(_user, "a", Build);
        await _cache.GetOrCreateAsync(_user, "b", Build);
        Assert.Equal(4, builds);
    }

    /// <summary>
    /// A write that lands while a value is being built must not leave that
    /// (now stale) value cached.
    /// </summary>
    [Fact]
    public async Task InvalidationDuringBuild_DoesNotCacheTheStaleValue()
    {
        var builds = 0;

        await _cache.GetOrCreateAsync(_user, "k", () =>
        {
            builds++;
            _cache.Invalidate(_user);
            return Task.FromResult("stale");
        });

        var next = await _cache.GetOrCreateAsync(_user, "k", () =>
        {
            builds++;
            return Task.FromResult("fresh");
        });

        Assert.Equal("fresh", next);
        Assert.Equal(2, builds);
    }
}
