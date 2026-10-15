using GolfProgress.Api.Services;
using GolfProgress.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace GolfProgress.Api.Tests;

/// <summary>
/// Regression guard for issue #20: audit events older than the retention period
/// (365 days by default) are purged, newer ones are kept.
/// </summary>
public sealed class EventRetentionTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    private ApiFactory _factory = null!;
    private Guid _userId;

    public async Task InitializeAsync()
    {
        _factory = new ApiFactory();
        _userId = Guid.NewGuid();

        await _factory.WithDbAsync(async db =>
        {
            db.Users.Add(TestWorld.NewUser(_userId, "events@example.test"));
            await db.SaveChangesAsync();
        });
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Purge_DeletesOnlyEventsOlderThanTheRetentionPeriod()
    {
        await SeedEventsAsync(
            ("ancient", Now.AddDays(-400)),
            ("just-expired", Now.AddDays(-365).AddMinutes(-1)),
            ("just-kept", Now.AddDays(-365).AddMinutes(1)),
            ("recent", Now.AddDays(-1)));

        var deleted = await PurgeAsync();

        Assert.Equal(2, deleted);
        _factory.WithDb(db => Assert.Equal(
            new[] { "just-kept", "recent" },
            db.Events.Select(e => e.EventType).OrderBy(t => t).ToArray()));
    }

    [Fact]
    public async Task Purge_RemovesABacklogLargerThanOneBatch()
    {
        var old = Enumerable.Range(0, EventRetentionService.BatchSize * 2 + 5)
            .Select(i => ($"old-{i}", Now.AddDays(-500).AddMinutes(i)))
            .ToArray();
        await SeedEventsAsync(old);

        var deleted = await PurgeAsync();

        Assert.Equal(old.Length, deleted);
        _factory.WithDb(db => Assert.Empty(db.Events));
    }

    [Fact]
    public async Task Purge_WithNothingToDelete_ReturnsZero()
    {
        await SeedEventsAsync(("recent", Now.AddDays(-1)));

        Assert.Equal(0, await PurgeAsync());
    }

    private async Task<int> PurgeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<EventRetentionService>()
            .PurgeAsync(Now, CancellationToken.None);
    }

    private Task SeedEventsAsync(params (string type, DateTimeOffset createdAt)[] events) =>
        _factory.WithDbAsync(async db =>
        {
            foreach (var (type, createdAt) in events)
            {
                db.Events.Add(new EventLog
                {
                    EventLogId = Guid.NewGuid(),
                    UserId = _userId,
                    EventType = type,
                    PayloadJson = JsonDocument.Parse("{}"),
                    CreatedAt = createdAt
                });
            }

            await db.SaveChangesAsync();
        });
}
