namespace GolfProgress.Api.Contracts.Auth;

public sealed class AuthResponse
{
    public Guid UserId { get; init; }
    public string Token { get; init; } = "";
    public string RefreshToken { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; init; }
}