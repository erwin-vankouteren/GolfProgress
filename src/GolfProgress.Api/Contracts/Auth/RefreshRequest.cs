namespace GolfProgress.Api.Contracts.Auth;

public sealed class RefreshRequest
{
    public string RefreshToken { get; init; } = "";
}