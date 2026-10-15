using GolfProgress.Api.Services;
using GolfProgress.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace GolfProgress.Api.Tests;

/// <summary>
/// Regression guard for issue #8. The compute path used to save three times, so a
/// failure partway through left the baseline, fitness score and event log
/// inconsistent with each other. It now saves once: all of them or none.
/// </summary>
public sealed class ComputeAtomicityTests : IAsyncLifetime
{
    private readonly FailWhenSavingEvents _failure = new();
    private ApiFactory _factory = null!;
    private Guid _userId;
    private Guid _roundId;

    public async Task InitializeAsync()
    {
        _factory = new ApiFactory(_failure);
        _userId = Guid.NewGuid();
        _roundId = Guid.NewGuid();

        await _factory.WithDbAsync(async db =>
        {
            db.Users.Add(TestWorld.NewUser(_userId, "atomic@example.test"));
            db.Rounds.Add(TestWorld.NewRound(_roundId, _userId));
            await db.SaveChangesAsync();
        });

        // Seeding saves through the interceptor too; count only the compute.
        _failure.Reset();
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Compute_WhenWritingEventsFails_WritesNothing()
    {
        _failure.Enabled = true;

        await Assert.ThrowsAsync<InvalidOperationException>(ComputeAsync);

        _factory.WithDb(db =>
        {
            Assert.Empty(db.FitnessScores.Where(fs => fs.RoundId == _roundId));
            Assert.Empty(db.UserBaselines.Where(b => b.UserId == _userId));
            Assert.Empty(db.Events.Where(e => e.UserId == _userId));
        });
    }

    [Fact]
    public async Task Compute_WhenSaveSucceeds_WritesEverythingTogether()
    {
        await ComputeAsync();

        _factory.WithDb(db =>
        {
            Assert.Single(db.FitnessScores.Where(fs => fs.RoundId == _roundId));
            Assert.Single(db.UserBaselines.Where(b => b.UserId == _userId));
            Assert.Equal(2, db.Events.Count(e => e.UserId == _userId));
        });

        // One save for the whole compute, not one per record.
        Assert.Equal(1, _failure.SavesWithEvents + _failure.SavesWithoutEvents);
    }

    private async Task ComputeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ProgressComputationService>();
        await service.ComputeForRoundAsync(_roundId, _userId, windowN: 5, CancellationToken.None);
    }

    /// <summary>
    /// Fails any save that includes event rows, standing in for a database error
    /// late in the compute. Also counts saves, so a test can see how many there were.
    /// </summary>
    private sealed class FailWhenSavingEvents : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }
        public int SavesWithEvents { get; private set; }
        public int SavesWithoutEvents { get; private set; }

        public void Reset() => SavesWithEvents = SavesWithoutEvents = 0;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
        {
            var hasEvents = eventData.Context!.ChangeTracker.Entries<EventLog>()
                .Any(e => e.State == EntityState.Added);

            if (hasEvents) SavesWithEvents++; else SavesWithoutEvents++;

            if (Enabled && hasEvents)
                throw new InvalidOperationException("Simulated failure while saving events.");

            return base.SavingChangesAsync(eventData, result, ct);
        }
    }
}
