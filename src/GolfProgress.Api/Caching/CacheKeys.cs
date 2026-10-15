namespace GolfProgress.Api.Caching;

/// <summary>
/// The single place cache keys are built (#15). A key must include every input
/// that changes the cached response, or one request is answered with another's data.
/// </summary>
public static class CacheKeys
{
    public static string Dashboard(Guid userId)
        => $"dashboard:{userId}";

    public static string Timeline(Guid userId, int take, DateOnly? from, DateOnly? to)
        => $"timeline:{userId}:take={take}:from={from:yyyy-MM-dd}:to={to:yyyy-MM-dd}";
}
