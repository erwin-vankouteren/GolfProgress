namespace GolfProgress.Api.Caching;

public static class CacheKeys
{
    public static string Dashboard(Guid userId)
        => $"dashboard:{userId}";

    public static string Progress(Guid userId)
        => $"progress:{userId}";

    public static string Timeline(Guid userId)
        => $"timeline:{userId}";
}