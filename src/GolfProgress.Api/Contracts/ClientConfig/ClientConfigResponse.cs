namespace GolfProgress.Api.Contracts.ClientConfig;

public sealed class ClientConfigResponse
{
    public MinimumVersionsResponse MinimumVersions { get; init; } = new();
}

/// <summary>
/// The oldest app version the API supports, per platform, as major.minor.patch. An app
/// older than this should show "update required".
/// </summary>
public sealed class MinimumVersionsResponse
{
    public string Ios { get; init; } = "";
    public string Android { get; init; } = "";
    public string Web { get; init; } = "";
}
