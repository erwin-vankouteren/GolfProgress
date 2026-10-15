using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using System.Collections.Concurrent;

namespace GolfProgress.Api.Caching;

/// <summary>
/// Caches per-user read models (dashboard, timeline) and drops all of a user's
/// entries at once when their data changes (#14).
///
/// IMemoryCache cannot remove by key prefix, and the timeline is cached under many
/// keys (one per take/from/to). Instead every entry for a user is tied to that
/// user's cancellation token; <see cref="Invalidate"/> cancels it, which evicts
/// every one of those entries, and later entries get a fresh token.
/// </summary>
public sealed class UserScopedCache
{
    /// <summary>Upper bound on staleness if an invalidation is ever missed.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(2);

    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _userTokens = new();

    public UserScopedCache(IMemoryCache cache) => _cache = cache;

    public async Task<T> GetOrCreateAsync<T>(Guid userId, string key, Func<Task<T>> create)
    {
        if (_cache.TryGetValue(key, out T? cached) && cached is not null)
            return cached;

        // Take the token BEFORE reading, so an invalidation that lands while the
        // value is being built still evicts it, rather than caching stale data.
        var token = _userTokens.GetOrAdd(userId, _ => new CancellationTokenSource()).Token;

        var value = await create();

        if (!token.IsCancellationRequested)
        {
            _cache.Set(key, value, new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(Lifetime)
                .AddExpirationToken(new CancellationChangeToken(token)));
        }

        return value;
    }

    /// <summary>Evicts everything cached for <paramref name="userId"/>.</summary>
    public void Invalidate(Guid userId)
    {
        if (_userTokens.TryRemove(userId, out var tokenSource))
        {
            tokenSource.Cancel();
            tokenSource.Dispose();
        }
    }
}
