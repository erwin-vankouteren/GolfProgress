using System.Security.Claims;

namespace GolfProgress.Api.Security;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The JWT "sid" claim: the id of the refresh token issued with the access token, so
    /// the API knows which session a request comes from (#83).
    /// </summary>
    public const string SessionIdClaim = "sid";

    /// <summary>
    /// The caller's session, or null for a token issued before sessions were recorded.
    /// Read under both names, since the handler may map "sid" to ClaimTypes.Sid.
    /// </summary>
    public static Guid? GetSessionId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(SessionIdClaim) ?? user.FindFirstValue(ClaimTypes.Sid);
        return Guid.TryParse(value, out var id) ? id : null;
    }

    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("Missing user id claim.");
        return Guid.Parse(value);
    }
}