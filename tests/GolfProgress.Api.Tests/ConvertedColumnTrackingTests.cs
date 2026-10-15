using GolfProgress.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace GolfProgress.Api.Tests;

/// <summary>
/// Regression guard for issue #12. Columns stored through a value converter need a
/// value comparer, or EF compares them by reference: edits to a list in place were
/// silently not saved, and an identical JSON document counted as a change.
/// </summary>
public sealed class ConvertedColumnTrackingTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly Guid _userId = Guid.NewGuid();

    public ConvertedColumnTrackingTests()
    {
        _factory.WithDbAsync(async db =>
        {
            db.Users.Add(TestWorld.NewUser(_userId, "tracking@example.test"));
            await db.SaveChangesAsync();
        }).GetAwaiter().GetResult();
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task StringList_EditedInPlace_IsSaved()
    {
        await _factory.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.UserId == _userId);
            user.GoalFocus.Add("putting");
            await db.SaveChangesAsync();
        });

        await _factory.WithDbAsync(async db =>
        {
            var user = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == _userId);
            Assert.Equal(new[] { "putting" }, user.GoalFocus);
        });
    }

    [Fact]
    public async Task JsonDocument_ReplacedWithIdenticalContent_IsNotAChange()
    {
        var eventId = Guid.NewGuid();

        await _factory.WithDbAsync(async db =>
        {
            db.Events.Add(new EventLog
            {
                EventLogId = eventId,
                UserId = _userId,
                EventType = "Test",
                PayloadJson = JsonDocument.Parse("{\"a\":1}"),
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        });

        await _factory.WithDbAsync(async db =>
        {
            var row = await db.Events.SingleAsync(e => e.EventLogId == eventId);

            row.PayloadJson = JsonDocument.Parse("{\"a\":1}");
            db.ChangeTracker.DetectChanges();
            Assert.Equal(EntityState.Unchanged, db.Entry(row).State);

            row.PayloadJson = JsonDocument.Parse("{\"a\":2}");
            db.ChangeTracker.DetectChanges();
            Assert.Equal(EntityState.Modified, db.Entry(row).State);

            await db.SaveChangesAsync();
        });

        await _factory.WithDbAsync(async db =>
        {
            var row = await db.Events.AsNoTracking().SingleAsync(e => e.EventLogId == eventId);
            Assert.Equal(2, row.PayloadJson.RootElement.GetProperty("a").GetInt32());
        });
    }
}
