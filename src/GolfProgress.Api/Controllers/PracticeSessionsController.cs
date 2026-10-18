using GolfProgress.Api.Contracts;
using GolfProgress.Api.OpenApi;
using GolfProgress.Api.Contracts.PracticeSessions;
using GolfProgress.Api.Security;
using GolfProgress.Domain.Entities;
using GolfProgress.Domain.Enums;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace GolfProgress.Api.Controllers;

/// <summary>
/// Practice sessions (#19). Every lookup by id is scoped to the caller, so another
/// user's session answers 404 exactly like a missing one.
/// </summary>
[Authorize]
[ApiController]
public sealed class PracticeSessionsController : ControllerBase
{
    private readonly GolfProgressDbContext _db;

    public PracticeSessionsController(GolfProgressDbContext db) => _db = db;

    // POST /me/practice-sessions
    [HttpPost("/me/practice-sessions", Name = "createPracticeSession")]
    [ProducesResponseType<PracticeSessionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ResponseHeader(StatusCodes.Status201Created, "Location", "string", "URL of the created practice session.")]
    public async Task<ActionResult<PracticeSessionResponse>> Create(
        [FromBody] CreatePracticeSessionRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var now = DateTimeOffset.UtcNow;

        // Both parse: CreatePracticeSessionRequest.Validate has already checked them.
        var session = new PracticeSession
        {
            PracticeSessionId = Guid.NewGuid(),
            UserId = userId,
            PracticedAt = IsoDate.Parse(request.PracticedAt),
            DurationMinutes = request.DurationMinutes,
            SessionType = request.ParsedSessionType,
            VenueName = request.VenueName,
            Tags = request.TagsOrNone.Select(t => t.Trim()).ToList(),
            PrimaryGoal = request.PrimaryGoal,
            Notes = request.Notes,
            CreatedAt = now
        };

        _db.PracticeSessions.Add(session);
        _db.Events.Add(new EventLog
        {
            EventLogId = Guid.NewGuid(),
            UserId = userId,
            EventType = "PracticeLogged",
            RelatedId = session.PracticeSessionId,
            PayloadJson = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                practiceSessionId = session.PracticeSessionId,
                practicedAt = session.PracticedAt.ToString("yyyy-MM-dd"),
                sessionType = session.SessionType.ToString()
            })),
            CreatedAt = now
        });

        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = session.PracticeSessionId }, ToResponse(session));
    }

    // GET /me/practice-sessions/{id}
    [HttpGet("/me/practice-sessions/{id:guid}", Name = "getPracticeSession")]
    [ProducesResponseType<PracticeSessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PracticeSessionResponse>> GetById(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var session = await _db.PracticeSessions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.PracticeSessionId == id && s.UserId == userId, ct);

        return session is null ? NotFound() : Ok(ToResponse(session));
    }

    // GET /me/practice-sessions?skip=0&take=20
    //
    // Paged and newest first, like GET /me/rounds: take is clamped to 1..100 and
    // the total is returned in X-Total-Count.
    [HttpGet("/me/practice-sessions", Name = "listMyPracticeSessions")]
    [ProducesResponseType<List<PracticeSessionResponse>>(StatusCodes.Status200OK)]
    [ResponseHeader(StatusCodes.Status200OK, "X-Total-Count", "integer", "Total number of the caller's practice sessions.")]
    public async Task<ActionResult<List<PracticeSessionResponse>>> List(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        skip = Math.Max(skip, 0);
        take = Math.Clamp(take, 1, 100);

        var query = _db.PracticeSessions.AsNoTracking().Where(s => s.UserId == userId);

        Response.Headers["X-Total-Count"] = (await query.CountAsync(ct)).ToString();

        var sessions = await query
            .OrderByDescending(s => s.PracticedAt)
            .ThenByDescending(s => s.CreatedAt)
            .ThenBy(s => s.PracticeSessionId)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        return Ok(sessions.Select(ToResponse).ToList());
    }

    // DELETE /me/practice-sessions/{id}
    [HttpDelete("/me/practice-sessions/{id:guid}", Name = "deletePracticeSession")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var session = await _db.PracticeSessions
            .FirstOrDefaultAsync(s => s.PracticeSessionId == id && s.UserId == userId, ct);
        if (session is null) return NotFound();

        _db.PracticeSessions.Remove(session);
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    private static PracticeSessionResponse ToResponse(PracticeSession s) => new()
    {
        PracticeSessionId = s.PracticeSessionId,
        UserId = s.UserId,
        PracticedAt = s.PracticedAt.ToString("yyyy-MM-dd"),
        DurationMinutes = s.DurationMinutes,
        SessionType = s.SessionType.ToString(),
        VenueName = s.VenueName,
        Tags = s.Tags,
        PrimaryGoal = s.PrimaryGoal,
        Notes = s.Notes,
        CreatedAt = s.CreatedAt
    };
}
