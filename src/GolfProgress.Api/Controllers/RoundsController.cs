using GolfProgress.Api.Configuration;
using GolfProgress.Api.Contracts.Rounds;
using GolfProgress.Api.Contracts;
using GolfProgress.Api.OpenApi;
using GolfProgress.Api.ErrorHandling;
using GolfProgress.Api.Security;
using GolfProgress.Api.Services;
using GolfProgress.Domain.Entities;
using GolfProgress.Domain.Enums;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace GolfProgress.Api.Controllers;

[Authorize]
[ApiController]
public sealed class RoundsController : ControllerBase
{
    private readonly GolfProgressDbContext _db;
    private readonly ProgressComputationService _progress;
    private readonly MicroWinsService _microWins;
    private readonly ProgressOptions _options;

    public RoundsController(
        GolfProgressDbContext db,
        ProgressComputationService progress,
        MicroWinsService microWins,
        IOptions<ProgressOptions> options)
    {
        _db = db;
        _progress = progress;
        _microWins = microWins;
        _options = options.Value;
    }

    // POST /me/rounds
    //
    // Creates the round and scores it: the fitness score, the current baseline and
    // the micro-wins are written in the same save as the round, so a logged round is
    // never left unscored (#80). The next lever stays on demand: it isn't stored,
    // and GET /me/dashboard and POST /rounds/{id}/next-lever compute it from the
    // round and the current baseline when asked.
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

        var now = DateTimeOffset.UtcNow;

        var round = new Round
        {
            RoundId = Guid.NewGuid(),
            UserId = userId,
            CreatedAt = now
        };
        Apply(request, playedAt, round, now);

        _db.Rounds.Add(round);
        // Event log (RoundLogged) — core to event-based model
        _db.Events.Add(RoundEvent("RoundLogged", round, now));

        var (baseline, fitness) = await _progress.StageForRoundAsync(round, _options.BaselineWindowRounds, ct);
        await _microWins.StageForRoundAsync(round, baseline, ct);

        // One save, so one transaction: the round, its score, the baseline, the
        // micro-wins and their events are all written, or none are (see #8).
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = round.RoundId }, ToResponse(round, fitness));
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

        var score = await _db.FitnessScores.AsNoTracking().FirstOrDefaultAsync(f => f.RoundId == id, ct);

        return Ok(ToResponse(round, score));
    }

    // PUT /rounds/{id}
    //
    // Replaces the round's details (#81), validated as when logging one. Its score,
    // its micro-wins and the current baseline are recomputed, and so are the scores of
    // the other rounds it was or now is an input to: see
    // ProgressComputationService.RoundsAffectedBy. All of it is one save.
    [HttpPut("/rounds/{id:guid}", Name = "updateRound")]
    [ProducesResponseType<RoundResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoundResponse>> Update(Guid id, [FromBody] CreateRoundRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var round = await _db.FindOwnedRoundAsync(id, userId, ct);
        if (round is null) return NotFound();

        if (!IsoDate.TryParse(request.PlayedAt, out var playedAt))
            return this.FieldError(nameof(request.PlayedAt), "PlayedAt must be an ISO date, yyyy-MM-dd.");

        var now = DateTimeOffset.UtcNow;
        var playedBefore = round.PlayedAt;

        Apply(request, playedAt, round, now);
        _db.Events.Add(RoundEvent("RoundUpdated", round, now));

        var baseline = await _progress.StageAfterRoundChangeAsync(
            userId, round.RoundId, round, new[] { playedBefore, round.PlayedAt }, _options.BaselineWindowRounds, ct);
        await _microWins.StageForRoundAsync(round, baseline, ct);

        await _db.SaveChangesAsync(ct);

        // The score staged above, now saved.
        var score = _db.FitnessScores.Local.FirstOrDefault(f => f.RoundId == round.RoundId);
        return Ok(ToResponse(round, score));
    }

    // DELETE /rounds/{id}
    //
    // Deletes the round with its fitness score, micro-wins and reflections (#81), then
    // rescores the rounds it was an input to and the current baseline, in one save.
    [HttpDelete("/rounds/{id:guid}", Name = "deleteRound")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var round = await _db.FindOwnedRoundAsync(id, userId, ct);
        if (round is null) return NotFound();

        // SQL Server cascades these from the round. Removing them explicitly as well
        // keeps the change tracker, and providers without the cascade, in step.
        _db.FitnessScores.RemoveRange(await _db.FitnessScores.Where(x => x.RoundId == id).ToListAsync(ct));
        _db.MicroWins.RemoveRange(await _db.MicroWins.Where(x => x.RoundId == id).ToListAsync(ct));
        _db.Reflections.RemoveRange(await _db.Reflections.Where(x => x.RoundId == id).ToListAsync(ct));
        _db.Rounds.Remove(round);

        _db.Events.Add(RoundEvent("RoundDeleted", round, DateTimeOffset.UtcNow));

        await _progress.StageAfterRoundChangeAsync(
            userId, round.RoundId, changed: null, new[] { round.PlayedAt }, _options.BaselineWindowRounds, ct);

        await _db.SaveChangesAsync(ct);

        return NoContent();
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
            .ToListAsync(ct);

        // The page's scores in one query, rather than one per round.
        var ids = rounds.Select(r => r.RoundId).ToList();
        var scores = await _db.FitnessScores.AsNoTracking()
            .Where(f => ids.Contains(f.RoundId))
            .ToDictionaryAsync(f => f.RoundId, ct);

        return Ok(rounds.Select(r => ToResponse(r, scores.GetValueOrDefault(r.RoundId))).ToList());
    }

    /// <summary>Copies a validated create or update request onto the round.</summary>
    private static void Apply(CreateRoundRequest request, DateOnly playedAt, Round round, DateTimeOffset now)
    {
        round.PlayedAt = playedAt;
        round.Holes = request.Holes;
        round.CourseName = request.CourseName;
        round.TeeName = request.TeeName;
        round.TotalStrokes = request.TotalStrokes;
        round.TotalPutts = request.TotalPutts;
        round.Penalties = request.Penalties;
        round.FairwaysHit = request.FairwaysHit;
        round.Gir = request.Gir;
        round.ThreePutts = request.ThreePutts;
        round.BlowUpHoles = request.BlowUpHoles;
        round.Source = Enum.TryParse<RoundSource>(request.Source, ignoreCase: true, out var source)
            ? source
            : RoundSource.Manual;
        round.Notes = request.Notes;
        round.UpdatedAt = now;
    }

    private static EventLog RoundEvent(string eventType, Round round, DateTimeOffset now) => new()
    {
        EventLogId = Guid.NewGuid(),
        UserId = round.UserId,
        EventType = eventType,
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

    private static RoundResponse ToResponse(Round r, FitnessScore? score = null) => new()
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
        UpdatedAt = r.UpdatedAt,
        FitnessScore = score?.ScoreTotal,
        FitnessConfidence = score?.ConfidenceLevel.ToString()
    };
}
