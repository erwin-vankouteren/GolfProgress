namespace GolfProgress.Api.Configuration;

/// <summary>
/// Browser origins allowed to call the API (#16). Empty by default: the mobile
/// client is not a browser and needs no CORS, so nothing is opened up until a
/// browser-based client's origin is listed here.
///
/// Named to avoid clashing with ASP.NET Core's own CorsOptions.
/// </summary>
public sealed class ClientCorsOptions
{
    public const string SectionName = "Cors";

    /// <summary>Exact origins, e.g. "https://app.example.com". No wildcards, paths or trailing slashes.</summary>
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Response headers browser clients need to read: paging totals, the location of
    /// a created resource, and how long to back off after a 429.
    /// </summary>
    public static readonly string[] ExposedHeaders = { "X-Total-Count", "Location", "Retry-After" };

    /// <summary>
    /// An origin is scheme, host and optional port only. Rejecting "*" keeps the
    /// policy an explicit allow-list.
    /// </summary>
    public static bool IsValidOrigin(string origin) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
        && uri.AbsolutePath == "/"
        && !origin.EndsWith('/')
        && string.IsNullOrEmpty(uri.Query)
        && string.IsNullOrEmpty(uri.Fragment)
        && string.IsNullOrEmpty(uri.UserInfo);
}
