using GolfProgress.Api.Configuration;
using GolfProgress.Api.Contracts.Progress;
using GolfProgress.Api.Security;
using GolfProgress.Api.Services;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GolfProgress.Api.Controllers;

[Authorize]
[ApiController]
public sealed class ProgressController : ControllerBase
{
    private readonly GolfProgressDbContext _db;
    private readonly ProgressComputationService _svc;
    private readonly ProgressOptions _options;

    public ProgressController(GolfProgressDbContext db, ProgressComputationService svc, IOptions<ProgressOptions> options)
    {
        _db = db;
        _svc = svc;
        _options = options.Value;
    }

    // POST /rounds/{roundId}/compute
    [HttpPost("/rounds/{roundId:guid}/compute")]
    public async Task<ActionResult> Compute(Guid roundId, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!await _db.OwnsRoundAsync(roundId, userId, ct)) return NotFound();

        await _svc.ComputeForRoundAsync(roundId, userId, windowN: _options.BaselineWindowRounds, ct);
        return NoContent();
    }

    // GET /me/baseline
    [HttpGet("/me/baseline")]
    public async Task<ActionResult<UserBaselineResponse>> GetBaseline(CancellationToken ct)
    {
        var userId = User.GetUserId();

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
        var userId = User.GetUserId();

        var fs = await _db.FitnessScores.AsNoTracking()
            .FirstOrDefaultAsync(x => x.RoundId == roundId && x.UserId == userId, ct);
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
