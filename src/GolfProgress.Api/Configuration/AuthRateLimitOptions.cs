using System.ComponentModel.DataAnnotations;

namespace GolfProgress.Api.Configuration;

/// <summary>
/// Throttling for the anonymous /auth/* endpoints, so that guessing passwords costs
/// time rather than being free (#6), without throttling the many real users a mobile
/// carrier or a club's Wi-Fi puts behind one IP address (#85). The reasoning is in
/// docs/architecture.md, "Rate limits".
/// </summary>
public sealed class AuthRateLimitOptions
{
    public const string SectionName = "RateLimiting:Auth";

    /// <summary>
    /// Sign-in requests (login and register) one client IP may make per
    /// <see cref="PerIpWindowSeconds"/>, sustained. The allowance refills steadily, one
    /// request at a time, up to <see cref="PerIpBurstLimit"/>.
    /// </summary>
    [Range(1, 10_000)]
    public int PerIpPermitLimit { get; set; } = 10;

    [Range(1, 86_400)]
    public int PerIpWindowSeconds { get; set; } = 60;

    /// <summary>
    /// Sign-in requests one client IP may make at once, after a quiet spell: a group
    /// signing up together at a club, say.
    /// </summary>
    [Range(1, 10_000)]
    public int PerIpBurstLimit { get; set; } = 30;

    /// <summary>
    /// Refresh and logout requests one client IP may make per
    /// <see cref="SessionPerIpWindowSeconds"/>. Every signed-in device refreshes every 15
    /// minutes, so this is sized for a carrier IP shared by many active users; refresh
    /// tokens are random 512-bit values, so there is nothing to guess.
    /// </summary>
    [Range(1, 100_000)]
    public int SessionPerIpPermitLimit { get; set; } = 300;

    [Range(1, 86_400)]
    public int SessionPerIpWindowSeconds { get; set; } = 60;

    /// <summary>
    /// Login attempts allowed against one email address per
    /// <see cref="PerEmailWindowSeconds"/>, whichever IPs they come from. This is what
    /// stops a single account being brute-forced from many addresses.
    /// </summary>
    [Range(1, 10_000)]
    public int PerEmailPermitLimit { get; set; } = 5;

    [Range(1, 86_400)]
    public int PerEmailWindowSeconds { get; set; } = 300;
}
