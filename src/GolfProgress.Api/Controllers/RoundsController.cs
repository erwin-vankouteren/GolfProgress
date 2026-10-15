using GolfProgress.Api.Contracts.Rounds;
using GolfProgress.Api.Contracts;
using GolfProgress.Api.OpenApi;
using GolfProgress.Api.ErrorHandling;
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
    [HttpPost("/me/rounds", Name = "createRound")]
    [ProducesResponseType<RoundResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ResponseHeader(StatusCodes.Status201Created, "Location", "string", "URL of the created round.")]
    public async Task<ActionResult<RoundResponse>> Create([FromBody] CreateRoundRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        // Ranges and hole-count bounds are validated on CreateRoundRequest before
        // this runs; [ApiController] answers 400 for anything invalid.
        if (!IsoDate.TryParse(request.PlayedAt, out var playedAt))
            return this.FieldError(nameof(request.PlayedAt), "PlayedAt must be an ISO date, yyyy-MM-dd.");

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
    [HttpGet("/rounds/{id:guid}", Name = "getRound")]
    [ProducesResponseType<RoundResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoundResponse>> GetById(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var round = await _db.FindOwnedRoundReadOnlyAsync(id, userId, ct);

        if (round is null) return NotFound();

        return Ok(ToResponse(round));
    }

    /// <summary>Response header carrying the total number of the caller's rounds.</summary>
    public const string TotalCountHeader = "X-Total-Count";

    // GET /me/rounds?skip=0&take=20
    //
    // Paged, newest first (#13). take is clamped to 1..100 like /me/timeline; the
    // total is returned in X-Total-Count so a client knows when it has them all.
    [HttpGet("/me/rounds", Name = "listMyRounds")]
    [ProducesResponseType<List<RoundResponse>>(StatusCodes.Status200OK)]
    [ResponseHeader(StatusCodes.Status200OK, TotalCountHeader, "integer", "Total number of the caller's rounds.")]
    public async Task<ActionResult<List<RoundResponse>>> GetRoundsForUser(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        skip = Math.Max(skip, 0);
        take = Math.Clamp(take, 1, 100);

        var query = _db.Rounds.AsNoTracking().Where(r => r.UserId == userId);

        Response.Headers[TotalCountHeader] = (await query.CountAsync(ct)).ToString();

        var rounds = await query
            .OrderByDescending(r => r.PlayedAt)
            // PlayedAt is a date, so same-day rounds tie. Paging needs a total
            // order, or a round could repeat on one page and be missing from the next.
            .ThenByDescending(r => r.CreatedAt)
            .ThenBy(r => r.RoundId)
            .Skip(skip)
            .Take(take)
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
