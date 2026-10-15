using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Runtime.CompilerServices;

namespace GolfProgress.Api.Caching;

/// <summary>
/// Invalidates a user's cached views whenever their data is saved (#14).
///
/// Hooking SaveChanges rather than individual endpoints means no write path can
/// forget to invalidate -- including ones added later. Any added, modified or
/// deleted entity with a UserId counts. Users are collected before the save and
/// invalidated only after it succeeds, so a reader cannot re-cache the old data in
/// between.
/// </summary>
public sealed class CacheInvalidationInterceptor : SaveChangesInterceptor
{
    private const string UserIdProperty = "UserId";

    private readonly UserScopedCache _cache;

    // Saves run concurrently on different contexts; this keeps each context's
    // pending users separate without holding the context alive.
    private readonly ConditionalWeakTable<DbContext, HashSet<Guid>> _pending = new();

    public CacheInvalidationInterceptor(UserScopedCache cache) => _cache = cache;

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Collect(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        Collect(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Flush(eventData.Context);
        return result;
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken ct = default)
    {
        Flush(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) =>
        Discard(eventData.Context);

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken ct = default)
    {
        Discard(eventData.Context);
        return Task.CompletedTask;
    }

    private void Collect(DbContext? context)
    {
        if (context is null) return;

        var users = _pending.GetOrCreateValue(context);

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            if (entry.Metadata.FindProperty(UserIdProperty) is null)
                continue;

            if (entry.Property(UserIdProperty).CurrentValue is Guid userId)
                users.Add(userId);
        }
    }

    private void Flush(DbContext? context)
    {
        if (context is null || !_pending.TryGetValue(context, out var users)) return;

        foreach (var userId in users)
            _cache.Invalidate(userId);

        _pending.Remove(context);
    }

    private void Discard(DbContext? context)
    {
        if (context is not null) _pending.Remove(context);
    }
}
