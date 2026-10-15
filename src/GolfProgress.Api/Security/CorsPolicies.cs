namespace GolfProgress.Api.Security;

/// <summary>Names of the CORS policies registered in Program.cs.</summary>
public static class CorsPolicies
{
    /// <summary>
    /// For /auth/*: the configured origins may send credentials (the HttpOnly refresh
    /// cookie, #68). The rest of the API uses the default policy, without credentials:
    /// it's authorised by the bearer token, not a cookie.
    /// </summary>
    public const string AuthWithCredentials = "auth-with-credentials";
}
