using GolfProgress.Api.Contracts.Timeline;
using GolfProgress.Api.Security;
using GolfProgress.Api.Services;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace GolfProgress.Api.Controllers;

[Authorize]
[ApiController]
public sealed class TimelineController : ControllerBase
{
    private readonly TimelineService _svc;
    private readonly GolfProgressDbContext _db;
    private readonly IMemoryCache _cache;

    public TimelineController(TimelineService svc, GolfProgressDbContext db, IMemoryCache cache)
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
        var cacheKey = $"timeline:{userId}:take={take}";
        DateOnly? fromDate = null;
        DateOnly? toDate = null;

        if (_cache.TryGetValue(cacheKey, out RoundTimelineResponse? cached))
            return Ok(cached);

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


        var result = await _svc.GetRoundTimelineAsync(
                    userId,
                    take,
                    null,
                    null,
                    ct);

        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(2));
        //var result = await _svc.GetRoundTimelineAsync(userId, take, fromDate, toDate, ct);
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