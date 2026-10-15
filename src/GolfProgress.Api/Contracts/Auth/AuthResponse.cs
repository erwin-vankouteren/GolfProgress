namespace GolfProgress.Api.Contracts.Auth;

public sealed class AuthResponse
{
    public Guid UserId { get; init; }
    public string Token { get; init; } = "";

    /// <summary>
    /// The refresh token, for clients that store it themselves (the mobile apps). Null
    /// when the request asked for it as an HttpOnly cookie (X-Refresh-Token-Mode: cookie),
    /// so the web app never sees it (#68).
    /// </summary>
    public string? RefreshToken { get; set; }

    public DateTimeOffset ExpiresAt { get; init; }
}
