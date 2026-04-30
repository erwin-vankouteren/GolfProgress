using GolfProgress.Api.Caching;
using GolfProgress.Api.Contracts.Dashboard;
using GolfProgress.Api.Security;
using GolfProgress.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace GolfProgress.Api.Controllers;

[Authorize]
[ApiController]
public sealed class DashboardController : ControllerBase
{
    private readonly DashboardService _svc;
    private readonly IMemoryCache _cache;

    public DashboardController(DashboardService svc, IMemoryCache cache)
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
            var cacheKey = $"dashboard:{userId}";
            if (_cache.TryGetValue(cacheKey, out DashboardResponse cached)) 
                return Ok(cached);
            var result = await _svc.BuildAsync(userId, ct);
            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(2));
            return Ok(result);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }
}