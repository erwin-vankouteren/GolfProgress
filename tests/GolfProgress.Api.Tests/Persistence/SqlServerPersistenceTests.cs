using GolfProgress.Api.Services;
using GolfProgress.Domain.Entities;
using GolfProgress.Domain.Enums;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace GolfProgress.Api.Tests.Persistence;

/// <summary>
/// Behaviour the in-memory provider cannot show, checked against a real SQL Server
/// with the real migrations applied (#33): column defaults, converters as stored,
/// unique and filtered indexes, delete behaviour, and query ordering.
///
/// The rest of the suite stays on the in-memory provider for speed; this is a thin
/// layer underneath it, not a replacement.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class SqlServerPersistenceTests
{
    private readonly SqlServerFixture _sql;

    public SqlServerPersistenceTests(SqlServerFixture sql) => _sql = sql;

    [SqlServerFact]
    public async Task Migrations_AllApply()
    {
        await using var db = _sql.CreateContext();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(db.Database.GetMigrations(), await db.Database.GetAppliedMigrationsAsync());
    }

    /// <summary>HasDefaultValueSql("sysutcdatetime()") fills timestamps left unset.</summary>
    [SqlServerFact]
    public async Task CreatedAt_LeftUnset_IsFilledByTheDatabaseDefault()
    {
        var userId = await SeedUserAsync();
        var roundId = Guid.NewGuid();

        await using (var db = _sql.CreateContext())
        {
            db.Rounds.Add(new Round
            {
                RoundId = roundId, UserId = userId, PlayedAt = new DateOnly(2026, 5, 1),
                Holes = 18, TotalStrokes = 90
                // CreatedAt and UpdatedAt deliberately not set.
            });
            await db.SaveChangesAsync();
        }

        await using (var db = _sql.CreateContext())
        {
            var round = await db.Rounds.SingleAsync(r => r.RoundId == roundId);
            Assert.InRange(round.CreatedAt, DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(1));
            Assert.InRange(round.UpdatedAt, DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(1));
        }
    }

    /// <summary>The List&lt;string&gt; and JsonDocument converters, as actually stored.</summary>
    [SqlServerFact]
    public async Task Converters_StoreJsonText_AndRoundTrip()
    {
        var userId = await SeedUserAsync(goalFocus: new() { "putting", "penalties" });
        var eventId = Guid.NewGuid();

        await using (var db = _sql.CreateContext())
        {
            db.Events.Add(new EventLog
            {
                EventLogId = eventId, UserId = userId, EventType = "Test",
                PayloadJson = JsonDocument.Parse("{\"strokes\":90,\"tags\":[\"a\"]}"),
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        await using (var db = _sql.CreateContext())
        {
            var storedGoalFocus = await db.Database
                .SqlQuery<string>($"SELECT GoalFocus AS Value FROM Users WHERE UserId = {userId}")
                .SingleAsync();
            Assert.Equal("[\"putting\",\"penalties\"]", storedGoalFocus);

            var user = await db.Users.SingleAsync(u => u.UserId == userId);
            Assert.Equal(new[] { "putting", "penalties" }, user.GoalFocus);

            var evt = await db.Events.SingleAsync(e => e.EventLogId == eventId);
            Assert.Equal(90, evt.PayloadJson.RootElement.GetProperty("strokes").GetInt32());
        }
    }

    /// <summary>
    /// Users.Email is unique among non-null values: EF creates a filtered unique
    /// index, which also needs QUOTED_IDENTIFIER ON to be written to.
    /// </summary>
    [SqlServerFact]
    public async Task Email_IsUniqueAmongNonNullValues()
    {
        var email = $"{Guid.NewGuid():N}@example.test";
        await SeedUserAsync(email: email);

        await Assert.ThrowsAsync<DbUpdateException>(() => SeedUserAsync(email: email));

        // Two users without an email do not collide.
        await SeedUserAsync(email: null);
        await SeedUserAsync(email: null);
    }

    [SqlServerFact]
    public async Task FitnessScore_IsUniquePerRound()
    {
        var userId = await SeedUserAsync();
        var roundId = await SeedRoundAsync(userId);

        await AddScoreAsync(userId, roundId);

        await Assert.ThrowsAsync<DbUpdateException>(() => AddScoreAsync(userId, roundId));
    }

    /// <summary>Deleting a round takes its score, reflections and micro-wins with it.</summary>
    [SqlServerFact]
    public async Task DeletingARound_CascadesToItsDerivedRows()
    {
        var userId = await SeedUserAsync();
        var roundId = await SeedRoundAsync(userId);
        await AddScoreAsync(userId, roundId);

        await using (var db = _sql.CreateContext())
        {
            db.Reflections.Add(new Reflection
            {
                ReflectionId = Guid.NewGuid(), UserId = userId, RoundId = roundId, CreatedAt = DateTimeOffset.UtcNow
            });
            db.MicroWins.Add(new MicroWin
            {
                MicroWinId = Guid.NewGuid(), UserId = userId, RoundId = roundId,
                Category = MicroWinCategory.Habit, Direction = MicroWinDirection.Stable,
                Title = "t", Message = "m", EvidenceJson = JsonDocument.Parse("{}"),
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        // Deleted with plain SQL, so the database's own cascade does the work,
        // not EF's client-side cascade of tracked entities.
        await using (var db = _sql.CreateContext())
            await db.Database.ExecuteSqlAsync($"DELETE FROM Rounds WHERE RoundId = {roundId}");

        await using (var db = _sql.CreateContext())
        {
            Assert.False(await db.FitnessScores.AnyAsync(f => f.RoundId == roundId));
            Assert.False(await db.Reflections.AnyAsync(r => r.RoundId == roundId));
            Assert.False(await db.MicroWins.AnyAsync(m => m.RoundId == roundId));
        }
    }

    /// <summary>Users -> Rounds is Restrict: a user with rounds cannot be deleted.</summary>
    [SqlServerFact]
    public async Task DeletingAUserWithRounds_IsRefused()
    {
        var userId = await SeedUserAsync();
        await SeedRoundAsync(userId);

        await using var db = _sql.CreateContext();

        await Assert.ThrowsAsync<SqlException>(() =>
            db.Database.ExecuteSqlAsync($"DELETE FROM Users WHERE UserId = {userId}"));
    }

    /// <summary>
    /// The case #33 demonstrated: rounds played on the same day tie on PlayedAt, and
    /// the in-memory provider returns them in insertion order, so removing the
    /// CreatedAt tiebreaker (#10) went unnoticed.
    ///
    /// Here six same-day rounds have ids chosen so SQL Server's clustered-index order
    /// is the reverse of creation order. The earliest-created round, a 200, must fall
    /// outside the window of the five most recent; with no tiebreaker SQL Server is
    /// free to pick it.
    /// </summary>
    [SqlServerFact]
    public async Task BaselineWindow_BreaksSameDayTiesByCreationTime()
    {
        var userId = await SeedUserAsync();
        var day = new DateOnly(2026, 5, 1);
        var created = new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);
        var userPart = userId.ToString("N")[..20];
        Guid latest = default;

        await using (var db = _sql.CreateContext())
        {
            for (var i = 0; i < 6; i++)
            {
                // SQL Server sorts uniqueidentifier by its last six bytes first, so
                // this makes the earliest-created round sort first in the index.
                var id = Guid.Parse($"{userPart[..8]}-{userPart[8..12]}-{userPart[12..16]}-{userPart[16..20]}-{i:D12}");
                db.Rounds.Add(new Round
                {
                    RoundId = id, UserId = userId, PlayedAt = day, Holes = 18,
                    TotalStrokes = (short)(i == 0 ? 200 : 90),
                    CreatedAt = created.AddMinutes(i), UpdatedAt = created.AddMinutes(i)
                });
                latest = id;
            }
            await db.SaveChangesAsync();
        }

        await using (var db = _sql.CreateContext())
            await new ProgressComputationService(db).ComputeForRoundAsync(latest, userId, windowN: 5, CancellationToken.None);

        await using (var db = _sql.CreateContext())
        {
            var baseline = await db.UserBaselines.SingleAsync(b => b.UserId == userId);
            Assert.Equal(90m, baseline.AvgScore);
        }
    }

    private async Task<Guid> SeedUserAsync(string? email = "", List<string>? goalFocus = null)
    {
        var userId = Guid.NewGuid();
        await using var db = _sql.CreateContext();

        db.Users.Add(new User
        {
            UserId = userId,
            Email = email == "" ? $"{userId:N}@example.test" : email,
            DisplayName = "persistence",
            Units = Units.Metric,
            TrackingMode = TrackingMode.Minimal,
            GoalFocus = goalFocus ?? new List<string>(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        return userId;
    }

    private async Task<Guid> SeedRoundAsync(Guid userId)
    {
        var roundId = Guid.NewGuid();
        await using var db = _sql.CreateContext();

        db.Rounds.Add(new Round
        {
            RoundId = roundId, UserId = userId, PlayedAt = new DateOnly(2026, 5, 1), Holes = 18, TotalStrokes = 90,
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        return roundId;
    }

    private async Task AddScoreAsync(Guid userId, Guid roundId)
    {
        await using var db = _sql.CreateContext();

        db.FitnessScores.Add(new FitnessScore
        {
            FitnessScoreId = Guid.NewGuid(), UserId = userId, RoundId = roundId,
            ExplanationJson = JsonDocument.Parse("{}"), ComputedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }
}
