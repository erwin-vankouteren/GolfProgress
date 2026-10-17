using GolfProgress.Api.Configuration;
using GolfProgress.Api.Contracts.Progress;
using GolfProgress.Api.Security;
using GolfProgress.Api.Services;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

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
    [HttpPost("/rounds/{roundId:guid}/compute", Name = "computeRound")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Compute(Guid roundId, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!await _db.OwnsRoundAsync(roundId, userId, ct)) return NotFound();

        await _svc.ComputeForRoundAsync(roundId, userId, windowN: _options.BaselineWindowRounds, ct);
        return NoContent();
    }

    // GET /me/baseline
    [HttpGet("/me/baseline", Name = "getMyBaseline")]
    [ProducesResponseType<UserBaselineResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserBaselineResponse>> GetBaseline(CancellationToken ct)
    {
        var userId = User.GetUserId();

        var b = await _db.UserBaselines.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId, ct);
        if (b is null) return NotFound();

        // The baseline is the most recent WindowRounds rounds, recomputed on every
        // round written, so it averages all of them until there are more.
        var rounds = await _db.Rounds.CountAsync(r => r.UserId == userId, ct);

        return Ok(new UserBaselineResponse
        {
            UserId = b.UserId,
            WindowRounds = b.WindowRounds,
            SampleSize = Math.Min(rounds, b.WindowRounds),
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
    [HttpGet("/rounds/{roundId:guid}/fitness-score", Name = "getFitnessScore")]
    [ProducesResponseType<FitnessScoreResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
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
            ComputedAt = fs.ComputedAt,
            Explanation = Explain(fs.ExplanationJson)
        });
    }

    /// <summary>
    /// The stored explanation (ProgressComputationService.UpsertFitnessScoreAsync), read
    /// leniently: scores computed before a field existed simply leave it null.
    /// </summary>
    internal static FitnessScoreExplanation Explain(JsonDocument json)
    {
        var root = json.RootElement;
        root.TryGetProperty("habits", out var habits);

        return new FitnessScoreExplanation
        {
            BaselineAvgScore = Number(root, "avgScore")?.GetDecimal(),
            BaselineStdDev = Number(root, "scoreStdDev")?.GetDecimal(),
            RoundsInHabitWindow = Number(habits, "roundsInWindow")?.GetInt32(),
            PracticeSessionsInHabitWindow = Number(habits, "practiceSessionsInWindow")?.GetInt32(),
            Reflected = habits.ValueKind == JsonValueKind.Object &&
                        habits.TryGetProperty("reflected", out var r) &&
                        r.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? r.GetBoolean()
                : null
        };
    }

    private static JsonElement? Number(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object &&
        parent.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.Number
            ? value
            : null;
}
