namespace GolfProgress.Api.Security;

/// <summary>Names of the rate-limiting policies registered in Program.cs.</summary>
public static class RateLimitPolicies
{
    /// <summary>
    /// Per-IP limit on signing in: /auth/login and /auth/register (#6). A token bucket,
    /// so a burst of real users behind one IP gets through (#85).
    /// </summary>
    public const string Auth = "auth";

    /// <summary>
    /// Per-IP limit on keeping a session: /auth/refresh and /auth/logout (#85). Separate
    /// and much higher, so the steady refreshes of many devices behind one IP neither
    /// hit a limit nor use up the sign-in allowance.
    /// </summary>
    public const string AuthSession = "auth-session";

    /// <summary>
    /// Per-IP limit on other anonymous endpoints, such as /client-config (#66). Kept
    /// separate from <see cref="Auth"/> so app launches can't use up the login limit.
    /// </summary>
    public const string Anonymous = "anonymous";
}
