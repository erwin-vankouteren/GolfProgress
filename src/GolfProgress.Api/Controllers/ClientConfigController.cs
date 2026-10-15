using GolfProgress.Api.Configuration;
using GolfProgress.Api.Contracts.ClientConfig;
using GolfProgress.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace GolfProgress.Api.Controllers;

/// <summary>
/// Configuration the apps read at start-up, before anyone signs in (#66).
/// </summary>
[ApiController]
[EnableRateLimiting(RateLimitPolicies.Anonymous)]
public sealed class ClientConfigController : ControllerBase
{
    /// <summary>
    /// Long enough to spare the API a request on every app launch, short enough that
    /// raising a minimum version takes effect within minutes.
    /// </summary>
    public const int CacheSeconds = 300;

    private readonly ClientConfigOptions _options;

    public ClientConfigController(IOptions<ClientConfigOptions> options) => _options = options.Value;

    // GET /client-config
    [HttpGet("/client-config", Name = "getClientConfig")]
    [ProducesResponseType<ClientConfigResponse>(StatusCodes.Status200OK)]
    [ResponseCache(Duration = CacheSeconds, Location = ResponseCacheLocation.Any)]
    public ActionResult<ClientConfigResponse> Get() => Ok(new ClientConfigResponse
    {
        MinimumVersions = new MinimumVersionsResponse
        {
            Ios = _options.MinimumVersions.Ios,
            Android = _options.MinimumVersions.Android,
            Web = _options.MinimumVersions.Web
        }
    });
}
