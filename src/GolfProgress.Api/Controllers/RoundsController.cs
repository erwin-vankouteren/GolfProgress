using GolfProgress.Api.Contracts.Rounds;
using GolfProgress.Api.Security;
using GolfProgress.Domain.Entities;
using GolfProgress.Domain.Enums;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace GolfProgress.Api.Controllers;

[Authorize]
[ApiController]
public sealed class RoundsController : ControllerBase
{
    private readonly GolfProgressDbContext _db;

    public RoundsController(GolfProgressDbContext db) => _db = db;

    // POST /me/rounds
    [HttpPost("/me/rounds")]
    public async Task<ActionResult<RoundResponse>> Create([FromBody] CreateRoundRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        // Minimal validation
        if (request.TotalStrokes <= 0) return BadRequest("TotalStrokes must be > 0.");
        if (request.Holes is not (9 or 18)) return BadRequest("Holes must be 9 or 18.");

        if (!DateOnly.TryParse(request.PlayedAt, out var playedAt))
            return BadRequest("PlayedAt must be ISO date format: yyyy-MM-dd");

        var source = Enum.TryParse<RoundSource>(request.Source, ignoreCase: true, out var s)
            ? s
            : RoundSource.Manual;

        var now = DateTimeOffset.UtcNow;

        var round = new Round
        {
            RoundId = Guid.NewGuid(),
            UserId = userId,
            PlayedAt = playedAt,
            Holes = request.Holes,
            CourseName = request.CourseName,
            TeeName = request.TeeName,
            TotalStrokes = request.TotalStrokes,
            TotalPutts = request.TotalPutts,
            Penalties = request.Penalties,
            FairwaysHit = request.FairwaysHit,
            Gir = request.Gir,
            ThreePutts = request.ThreePutts,
            BlowUpHoles = request.BlowUpHoles,
            Source = source,
            Notes = request.Notes,
            CreatedAt = now,
            UpdatedAt = now
        };

        // Event log (RoundLogged) — core to event-based model
        var evt = new EventLog
        {
            EventLogId = Guid.NewGuid(),
            UserId = userId,
            EventType = "RoundLogged",
            RelatedId = round.RoundId,
            PayloadJson = JsonDocument.Parse(
                JsonSerializer.Serialize(new
                {
                    roundId = round.RoundId,
                    userId = round.UserId,
                    playedAt = round.PlayedAt.ToString("yyyy-MM-dd"),
                    holes = round.Holes,
                    totalStrokes = round.TotalStrokes
                })
            ),
            CreatedAt = now
        };

        _db.Rounds.Add(round);
        _db.Events.Add(evt);

        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = round.RoundId }, ToResponse(round));
    }

    // GET /rounds/{id}
    [HttpGet("/rounds/{id:guid}")]
    public async Task<ActionResult<RoundResponse>> GetById(Guid id, CancellationToken ct)
    {
        var round = await _db.Rounds.AsNoTracking()
            .FirstOrDefaultAsync(r => r.RoundId == id, ct);

        if (round is null) return NotFound();

        return Ok(ToResponse(round));
    }

    // GET /me/rounds
    [HttpGet("/me/rounds")]
    public async Task<ActionResult<List<RoundResponse>>> GetRoundsForUser(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var rounds = await _db.Rounds.AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.PlayedAt)
            .Select(r => ToResponse(r))
            .ToListAsync(ct);

        return Ok(rounds);
    }

    private static RoundResponse ToResponse(Round r) => new()
    {
        RoundId = r.RoundId,
        UserId = r.UserId,
        PlayedAt = r.PlayedAt.ToString("yyyy-MM-dd"),
        Holes = r.Holes,
        CourseName = r.CourseName,
        TeeName = r.TeeName,
        TotalStrokes = r.TotalStrokes,
        TotalPutts = r.TotalPutts,
        Penalties = r.Penalties,
        FairwaysHit = r.FairwaysHit,
        Gir = r.Gir,
        ThreePutts = r.ThreePutts,
        BlowUpHoles = r.BlowUpHoles,
        Source = r.Source.ToString(),
        Notes = r.Notes,
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt
    };
}
