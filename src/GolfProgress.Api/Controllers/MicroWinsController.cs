using GolfProgress.Api.Contracts.MicroWins;
using GolfProgress.Api.Services;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GolfProgress.Api.Controllers;

[Authorize]
[ApiController]
public sealed class MicroWinsController : ControllerBase
{
    private readonly MicroWinsService _svc;
    private readonly GolfProgressDbContext _db;

    public MicroWinsController(MicroWinsService svc, GolfProgressDbContext db)
    {
        _svc = svc;
        _db = db;
    }

    // POST /rounds/{roundId}/micro-wins/generate
    [HttpPost("/rounds/{roundId:guid}/micro-wins/generate")]
    public async Task<ActionResult<GenerateMicroWinsResponse>> Generate(Guid roundId, CancellationToken ct)
    {
        var wins = await _svc.GenerateForRoundAsync(roundId, ct);

        return Ok(new GenerateMicroWinsResponse
        {
            RoundId = roundId,
            GeneratedCount = wins.Count,
            Items = wins.Select(ToResponse).ToList()
        });
    }

    // GET /rounds/{roundId}/micro-wins
    [HttpGet("/rounds/{roundId:guid}/micro-wins")]
    public async Task<ActionResult<List<MicroWinResponse>>> Get(Guid roundId, CancellationToken ct)
    {
        var items = await _db.MicroWins.AsNoTracking()
            .Where(mw => mw.RoundId == roundId)
            .OrderBy(mw => mw.CreatedAt)
            .ToListAsync(ct);

        return Ok(items.Select(ToResponse).ToList());
    }

    private static MicroWinResponse ToResponse(GolfProgress.Domain.Entities.MicroWin mw) => new()
    {
        MicroWinId = mw.MicroWinId,
        UserId = mw.UserId,
        RoundId = mw.RoundId,
        Category = mw.Category.ToString(),
        Metric = mw.Metric,
        Direction = mw.Direction.ToString(),
        DeltaValue = mw.DeltaValue,
        ConfidenceLevel = mw.ConfidenceLevel.ToString(),
        Title = mw.Title,
        Message = mw.Message,
        CreatedAt = mw.CreatedAt
    };
}