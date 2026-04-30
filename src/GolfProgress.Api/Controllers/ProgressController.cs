using GolfProgress.Api.Caching;
using GolfProgress.Api.Contracts.Progress;
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
public sealed class ProgressController : ControllerBase
{
    private readonly GolfProgressDbContext _db;
    private readonly ProgressComputationService _svc;
    private readonly IMemoryCache _cache;

    public ProgressController(GolfProgressDbContext db, ProgressComputationService svc, IMemoryCache cache)
    {
        _db = db;
        _svc = svc;
        _cache = cache;
    }

    // POST /rounds/{roundId}/compute
    [HttpPost("/rounds/{roundId:guid}/compute")]
    public async Task<ActionResult> Compute(Guid roundId, CancellationToken ct)
    {
        await _svc.ComputeForRoundAsync(roundId, windowN: 5, ct);
        return NoContent();
    }

    [Authorize(Roles ="Admin")]
    // GET /me/baseline
    [HttpGet("/me/baseline")]
    public async Task<ActionResult<UserBaselineResponse>> GetBaseline(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var cacheKey = $"baseline:{userId}";

        if (_cache.TryGetValue(cacheKey, out UserBaselineResponse cached))
            return Ok(cached);


        var b = await _db.UserBaselines.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId, ct);
        if (b is null) return NotFound();

        return Ok(new UserBaselineResponse
        {
            UserId = b.UserId,
            WindowRounds = b.WindowRounds,
            AvgScore = b.AvgScore,
            ScoreStdDev = b.ScoreStdDev,
            AvgPutts = b.AvgPutts,
            AvgPenalties = b.AvgPenalties,
            AvgThreePutts = b.AvgThreePutts,
            AvgBlowUpHoles = b.AvgBlowUpHoles,
            AvgFairwaysHit = b.AvgFairwaysHit,
            AvgGir = b.AvgGir,
            CompletenessRatio = b.CompletenessRatio,
            ConfidenceLevel = b.ConfidenceLevel.ToString(),
            ComputedAt = b.ComputedAt
        });
    }

    // GET /rounds/{roundId}/fitness-score
    [HttpGet("/rounds/{roundId:guid}/fitness-score")]
    public async Task<ActionResult<FitnessScoreResponse>> GetFitnessScore(Guid roundId, CancellationToken ct)
    {
        var fs = await _db.FitnessScores.AsNoTracking().FirstOrDefaultAsync(x => x.RoundId == roundId, ct);
        if (fs is null) return NotFound();

        return Ok(new FitnessScoreResponse
        {
            FitnessScoreId = fs.FitnessScoreId,
            UserId = fs.UserId,
            RoundId = fs.RoundId,
            ScoreTotal = fs.ScoreTotal,
            Execution = fs.Execution,
            DamageControl = fs.DamageControl,
            Consistency = fs.Consistency,
            Habits = fs.Habits,
            ConfidenceLevel = fs.ConfidenceLevel.ToString(),
            ComputedAt = fs.ComputedAt
        });
    }
}
