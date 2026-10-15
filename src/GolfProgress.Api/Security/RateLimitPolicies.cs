namespace GolfProgress.Api.Security;

/// <summary>Names of the rate-limiting policies registered in Program.cs.</summary>
public static class RateLimitPolicies
{
    /// <summary>Per-IP limit on the anonymous /auth/* endpoints (#6).</summary>
    public const string Auth = "auth";

    /// <summary>
    /// Per-IP limit on other anonymous endpoints, such as /client-config (#66). Kept
    /// separate from <see cref="Auth"/> so app launches can't use up the login limit.
    /// </summary>
    public const string Anonymous = "anonymous";
}
