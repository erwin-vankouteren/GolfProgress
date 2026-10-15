using GolfProgress.Api.Contracts.Recommendations;
using GolfProgress.Api.Security;
using GolfProgress.Api.Services;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    [HttpPost("/rounds/{roundId:guid}/next-lever", Name = "recommendNextLever")]
    [ProducesResponseType<NextLeverResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NextLeverResponse>> Recommend(Guid roundId, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var round = await _db.FindOwnedRoundReadOnlyAsync(roundId, userId, ct);
        if (round is null) return NotFound();

        var (leverKey, title, rec, rationale, confidence) = await _svc.RecommendAsync(roundId, userId, ct);
        await _svc.LogEventAsync(userId, roundId, leverKey, ct);

        return Ok(new NextLeverResponse
        {
            RoundId = roundId,
            UserId = userId,
            LeverKey = leverKey,
            Title = title,
            Recommendation = rec,
            Rationale = rationale,
            ConfidenceLevel = confidence.ToString(),
            CreatedAt = DateTimeOffset.UtcNow
        });
    }
}
