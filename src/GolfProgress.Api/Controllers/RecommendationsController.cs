using GolfProgress.Api.Contracts.Recommendations;
using GolfProgress.Api.Services;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GolfProgress.Api.Controllers;

[Authorize]
[ApiController]
public sealed class RecommendationsController : ControllerBase
{
    private readonly NextLeverService _svc;
    private readonly GolfProgressDbContext _db;

    public RecommendationsController(NextLeverService svc, GolfProgressDbContext db)
    {
        _svc = svc;
        _db = db;
    }

    // POST /rounds/{roundId}/next-lever
    [HttpPost("/rounds/{roundId:guid}/next-lever")]
    public async Task<ActionResult<NextLeverResponse>> Recommend(Guid roundId, CancellationToken ct)
    {
        var round = await _db.Rounds.AsNoTracking().FirstOrDefaultAsync(r => r.RoundId == roundId, ct);
        if (round is null) return NotFound();

        var (leverKey, title, rec, rationale, confidence) = await _svc.RecommendAsync(roundId, ct);
        await _svc.LogEventAsync(round.UserId, roundId, leverKey, ct);

        return Ok(new NextLeverResponse
        {
            RoundId = roundId,
            UserId = round.UserId,
            LeverKey = leverKey,
            Title = title,
            Recommendation = rec,
            Rationale = rationale,
            ConfidenceLevel = confidence.ToString(),
            CreatedAt = DateTimeOffset.UtcNow
        });
    }
}