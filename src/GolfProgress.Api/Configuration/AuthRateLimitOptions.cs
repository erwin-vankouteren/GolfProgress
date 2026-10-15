using System.ComponentModel.DataAnnotations;

namespace GolfProgress.Api.Configuration;

/// <summary>
/// Throttling for the anonymous /auth/* endpoints, so that guessing passwords or
/// refresh tokens costs time rather than being free (#6).
/// </summary>
public sealed class AuthRateLimitOptions
{
    public const string SectionName = "RateLimiting:Auth";

    /// <summary>Requests one client IP may make to /auth/* per <see cref="PerIpWindowSeconds"/>.</summary>
    [Range(1, 10_000)]
    public int PerIpPermitLimit { get; set; } = 10;

    [Range(1, 86_400)]
    public int PerIpWindowSeconds { get; set; } = 60;

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
