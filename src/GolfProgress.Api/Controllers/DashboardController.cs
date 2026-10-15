using GolfProgress.Api.Caching;
using GolfProgress.Api.Contracts.Dashboard;
using GolfProgress.Api.Security;
using GolfProgress.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GolfProgress.Api.Controllers;

[Authorize]
[ApiController]
public sealed class DashboardController : ControllerBase
{
    private readonly DashboardService _svc;
    private readonly UserScopedCache _cache;

    public DashboardController(DashboardService svc, UserScopedCache cache)
    {
        _svc = svc;
        _cache = cache;
    }

    // GET /me/dashboard
    [HttpGet("/me/dashboard")]
    public async Task<ActionResult<DashboardResponse>> Get(CancellationToken ct)
    {
        try
        {
            var userId = User.GetUserId();
            var result = await _cache.GetOrCreateAsync(
                userId, CacheKeys.Dashboard(userId), () => _svc.BuildAsync(userId, ct));
            return Ok(result);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }
}