using GolfProgress.Api.Contracts.Timeline;
using GolfProgress.Api.Security;
using GolfProgress.Api.Services;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GolfProgress.Api.Caching;

namespace GolfProgress.Api.Controllers;

[Authorize]
[ApiController]
public sealed class TimelineController : ControllerBase
{
    private readonly TimelineService _svc;
    private readonly GolfProgressDbContext _db;
    private readonly UserScopedCache _cache;

    public TimelineController(TimelineService svc, GolfProgressDbContext db, UserScopedCache cache)
    {
        _svc = svc;
        _db = db;
        _cache = cache;
    }

    // GET /me/timeline?take=20&from=2026-01-01&to=2026-12-31
    [HttpGet("/me/timeline")]
    public async Task<ActionResult<RoundTimelineResponse>> GetRoundTimeline(
        [FromQuery] int take = 20,
        [FromQuery] string? from = null,
        [FromQuery] string? to = null,
        CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        DateOnly? fromDate = null;
        DateOnly? toDate = null;

        // Clamped here as well as in the service, so out-of-range values share one
        // cache entry instead of each creating their own.
        take = Math.Clamp(take, 1, 100);

        if (!string.IsNullOrWhiteSpace(from))
        {
            if (!DateOnly.TryParse(from, out var f)) return BadRequest("from must be yyyy-MM-dd");
            fromDate = f;
        }

        if (!string.IsNullOrWhiteSpace(to))
        {
            if (!DateOnly.TryParse(to, out var t)) return BadRequest("to must be yyyy-MM-dd");
            toDate = t;
        }

        // The dates used to be parsed and then dropped -- the service was called with
        // null, null -- and the cache key left them out too, so ?from/?to never
        // filtered anything. Both are now applied and part of the key.
        var result = await _cache.GetOrCreateAsync(
            userId,
            CacheKeys.Timeline(userId, take, fromDate, toDate),
            () => _svc.GetRoundTimelineAsync(userId, take, fromDate, toDate, ct));

        return Ok(result);
    }

    // GET /me/timeline/monthly?months=6
    [HttpGet("/me/timeline/monthly")]
    public async Task<ActionResult<MonthlyTimelineResponse>> GetMonthlyTimeline(
        [FromQuery] int months = 6,
        CancellationToken ct = default)
    {
        var userId = User.GetUserId();

        var result = await _svc.GetMonthlyTimelineAsync(userId, months, ct);
        return Ok(result);
    }
}