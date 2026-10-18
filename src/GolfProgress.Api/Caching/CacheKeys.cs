using GolfProgress.Domain.Enums;

namespace GolfProgress.Api.Caching;

/// <summary>
/// The single place cache keys are built (#15). A key must include every input
/// that changes the cached response, or one request is answered with another's data.
/// </summary>
public static class CacheKeys
{
    /// <summary>Whether the user still exists, checked on every authenticated request (#84).</summary>
    public static string UserExists(Guid userId)
        => $"user-exists:{userId}";

    // Both carry texts in the request's language, so the language is part of the key.
    public static string Dashboard(Guid userId, AppLanguage language)
        => $"dashboard:{userId}:lang={language}";

    public static string Timeline(Guid userId, int take, DateOnly? from, DateOnly? to, AppLanguage language)
        => $"timeline:{userId}:take={take}:from={from:yyyy-MM-dd}:to={to:yyyy-MM-dd}:lang={language}";
}
