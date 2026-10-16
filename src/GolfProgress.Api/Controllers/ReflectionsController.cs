using GolfProgress.Api.Contracts.Reflections;
using GolfProgress.Api.Security;
using GolfProgress.Domain.Entities;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace GolfProgress.Api.Controllers;

[Authorize]
[ApiController]
public sealed class ReflectionsController : ControllerBase
{
    private readonly GolfProgressDbContext _db;

    public ReflectionsController(GolfProgressDbContext db) => _db = db;

    // POST /rounds/{roundId}/reflections
    [HttpPost("/rounds/{roundId:guid}/reflections", Name = "createReflection")]
    [ProducesResponseType<ReflectionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReflectionResponse>> Create(Guid roundId, [FromBody] CreateReflectionRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var round = await _db.FindOwnedRoundAsync(roundId, userId, ct);
        if (round is null) return Problem(statusCode: StatusCodes.Status404NotFound, detail: $"Round {roundId} not found.");

        var now = DateTimeOffset.UtcNow;

        var reflection = new Reflection
        {
            ReflectionId = Guid.NewGuid(),
            UserId = userId,
            RoundId = roundId,
            FocusRating = request.FocusRating,
            MoodRating = request.MoodRating,
            FrustrationSpillover = request.FrustrationSpillover,
            WhatWorked = Text(request.WhatWorked),
            WhatToRepeat = Text(request.WhatToRepeat),
            OneChangeNext = Text(request.OneChangeNext),
            CreatedAt = now
        };

        _db.Reflections.Add(reflection);

        _db.Events.Add(new EventLog
        {
            EventLogId = Guid.NewGuid(),
            UserId = userId,
            EventType = "ReflectionAdded",
            RelatedId = roundId,
            PayloadJson = JsonDocument.Parse("{\"reflectionId\":\"" + reflection.ReflectionId + "\"}", new JsonDocumentOptions()),
            CreatedAt = now
        });

        await _db.SaveChangesAsync(ct);

        return Ok(ToResponse(reflection));
    }

    /// <summary>Trimmed, with blank stored as nothing rather than as spaces.</summary>
    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // GET /rounds/{roundId}/reflections
    [HttpGet("/rounds/{roundId:guid}/reflections", Name = "listReflections")]
    [ProducesResponseType<List<ReflectionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<ReflectionResponse>>> List(Guid roundId, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!await _db.OwnsRoundAsync(roundId, userId, ct)) return NotFound();

        var items = await _db.Reflections.AsNoTracking()
            .Where(r => r.RoundId == roundId && r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => ToResponse(r))
            .ToListAsync(ct);

        return Ok(items);
    }

    private static ReflectionResponse ToResponse(Reflection r) => new()
    {
        ReflectionId = r.ReflectionId,
        UserId = r.UserId,
        RoundId = r.RoundId,
        FocusRating = r.FocusRating,
        MoodRating = r.MoodRating,
        FrustrationSpillover = r.FrustrationSpillover,
        WhatWorked = r.WhatWorked,
        WhatToRepeat = r.WhatToRepeat,
        OneChangeNext = r.OneChangeNext,
        CreatedAt = r.CreatedAt
    };
}
