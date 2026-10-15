namespace GolfProgress.Api.Contracts.Auth;

public sealed class RefreshRequest
{
    /// <summary>
    /// The refresh token, from the mobile apps. Web clients leave it out: the API reads
    /// the HttpOnly refresh cookie instead (#68).
    /// </summary>
    public string? RefreshToken { get; init; }
}
