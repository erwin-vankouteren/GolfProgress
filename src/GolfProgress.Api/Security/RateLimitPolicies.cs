namespace GolfProgress.Api.Security;

/// <summary>Names of the rate-limiting policies registered in Program.cs.</summary>
public static class RateLimitPolicies
{
    /// <summary>Per-IP limit on the anonymous /auth/* endpoints (#6).</summary>
    public const string Auth = "auth";
}
