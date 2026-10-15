using System.ComponentModel.DataAnnotations;

namespace GolfProgress.Api.Configuration;

/// <summary>
/// What GET /client-config tells the apps (#66). Installed mobile apps can't be forced
/// to update, so the API publishes the oldest version it still supports; an app below
/// it shows "update required" instead of failing in unpredictable ways.
/// </summary>
public sealed class ClientConfigOptions
{
    public const string SectionName = "ClientConfig";

    [Required]
    public MinimumVersionOptions MinimumVersions { get; set; } = new();
}

public sealed class MinimumVersionOptions
{
    /// <summary>
    /// major.minor.patch, e.g. 1.0.0. Checked in Program.cs: ValidateDataAnnotations
    /// does not descend into nested objects like this one.
    /// </summary>
    public const string Pattern = @"^\d+\.\d+\.\d+$";

    public string Ios { get; set; } = "1.0.0";
    public string Android { get; set; } = "1.0.0";
    public string Web { get; set; } = "1.0.0";
}

/// <summary>Per-IP throttling for anonymous endpoints other than /auth/*.</summary>
public sealed class AnonymousRateLimitOptions
{
    public const string SectionName = "RateLimiting:Anonymous";

    [Range(1, 10_000)]
    public int PerIpPermitLimit { get; set; } = 60;

    [Range(1, 86_400)]
    public int PerIpWindowSeconds { get; set; } = 60;
}
