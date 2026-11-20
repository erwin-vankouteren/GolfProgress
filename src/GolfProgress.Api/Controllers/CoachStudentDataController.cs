using GolfProgress.Api.Contracts;
using GolfProgress.Api.Contracts.MicroWins;
using GolfProgress.Api.Contracts.MonthlyReviews;
using GolfProgress.Api.Contracts.PracticeSessions;
using GolfProgress.Api.Contracts.Progress;
using GolfProgress.Api.Contracts.Recommendations;
using GolfProgress.Api.Contracts.Reflections;
using GolfProgress.Api.Contracts.Rounds;
using GolfProgress.Api.Contracts.Timeline;
using GolfProgress.Api.ErrorHandling;
using GolfProgress.Api.OpenApi;
using GolfProgress.Api.Security;
using GolfProgress.Api.Services;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GolfProgress.Api.Controllers;

/// <summary>
/// A student's rounds, practice and progress, read-only, for their coach (#205,
/// docs/coach-access.md → "API"). The same contracts as the golfer's own endpoints, on
/// routes of their own, so no golfer endpoint changes how it authorises. Every action
/// starts with <see cref="CoachAccess"/>: without an active link, or when the golfer
/// doesn't share what's asked for, it's a 404, so a coach can't tell what exists.
/// </summary>
[Authorize]
[ApiController]
[Route("/coach/students/{golferId:guid}")]
public sealed class CoachStudentDataController : ControllerBase
{
    private readonly GolfProgressDbContext _db;
    private readonly CoachAccess _access;
    private readonly TimelineService _timeline;
    private readonly NextLeverService _nextLever;

    public CoachStudentDataController(
        GolfProgressDbContext db, CoachAccess access, TimelineService timeline, NextLeverService nextLever)
    {
        _db = db;
        _access = access;
        _timeline = timeline;
        _nextLever = nextLever;
    }

    // GET /coach/students/{golferId}/rounds?skip=0&take=20
    //
    // As GET /me/rounds; the notes only when reflections are shared.
    [HttpGet("rounds", Name = "listStudentRounds")]
    [ProducesResponseType<List<RoundResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ResponseHeader(StatusCodes.Status200OK, RoundsController.TotalCountHeader, "integer", "Total number of the student's rounds.")]
    public async Task<ActionResult<List<RoundResponse>>> Rounds(
        Guid golferId, [FromQuery] int skip = 0, [FromQuery] int take = 20, CancellationToken ct = default)
    {
        if (await LinkAsync(golferId, CoachShare.RoundsAndProgress, ct) is not { } link) return NotFound();

        var (rounds, total) = await RoundsController.PageAsync(
            _db, golferId, skip, take, CoachAccess.Covers(link, CoachShare.Reflections), ct);
        Response.Headers[RoundsController.TotalCountHeader] = total.ToString();
        return Ok(rounds);
    }

    // GET /coach/students/{golferId}/rounds/{roundId}
    [HttpGet("rounds/{roundId:guid}", Name = "getStudentRound")]
    [ProducesResponseType<RoundResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoundResponse>> Round(Guid golferId, Guid roundId, CancellationToken ct)
    {
        if (await LinkAsync(golferId, CoachShare.RoundsAndProgress, ct) is not { } link) return NotFound();

        var round = await _db.FindOwnedRoundReadOnlyAsync(roundId, golferId, ct);
        if (round is null) return NotFound();

        var score = await _db.FitnessScores.AsNoTracking().FirstOrDefaultAsync(f => f.RoundId == roundId, ct);
        return Ok(RoundsController.ToResponse(round, score, CoachAccess.Covers(link, CoachShare.Reflections)));
    }

    // GET /coach/students/{golferId}/rounds/{roundId}/fitness-score
    [HttpGet("rounds/{roundId:guid}/fitness-score", Name = "getStudentFitnessScore")]
    [ProducesResponseType<FitnessScoreResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FitnessScoreResponse>> FitnessScore(Guid golferId, Guid roundId, CancellationToken ct)
    {
        if (await LinkAsync(golferId, CoachShare.RoundsAndProgress, ct) is null) return NotFound();

        var fs = await _db.FitnessScores.AsNoTracking()
            .FirstOrDefaultAsync(x => x.RoundId == roundId && x.UserId == golferId, ct);
        return fs is null ? NotFound() : Ok(ProgressController.FitnessScoreResponse(fs));
    }

    // GET /coach/students/{golferId}/rounds/{roundId}/micro-wins
    [HttpGet("rounds/{roundId:guid}/micro-wins", Name = "listStudentMicroWins")]
    [ProducesResponseType<List<MicroWinResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<MicroWinResponse>>> MicroWins(Guid golferId, Guid roundId, CancellationToken ct)
    {
        if (await LinkAsync(golferId, CoachShare.RoundsAndProgress, ct) is null) return NotFound();
        if (!await _db.OwnsRoundAsync(roundId, golferId, ct)) return NotFound();

        var items = await _db.MicroWins.AsNoTracking()
            .Where(mw => mw.RoundId == roundId && mw.UserId == golferId)
            .OrderBy(mw => mw.CreatedAt)
            .ToListAsync(ct);
        return Ok(items.Select(MicroWinResponse.From).ToList());
    }

    // GET /coach/students/{golferId}/rounds/{roundId}/next-lever
    //
    // As GET /rounds/{roundId}/next-lever: computed, not stored, and nothing is logged.
    [HttpGet("rounds/{roundId:guid}/next-lever", Name = "getStudentNextLever")]
    [ProducesResponseType<NextLeverResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NextLeverResponse>> NextLever(Guid golferId, Guid roundId, CancellationToken ct)
    {
        if (await LinkAsync(golferId, CoachShare.RoundsAndProgress, ct) is null) return NotFound();
        if (!await _db.OwnsRoundAsync(roundId, golferId, ct)) return NotFound();

        var (leverKey, title, rec, rationale, confidence) = await _nextLever.RecommendAsync(roundId, golferId, ct);
        return Ok(new NextLeverResponse
        {
            RoundId = roundId,
            UserId = golferId,
            LeverKey = leverKey,
            Title = title,
            Recommendation = rec,
            Rationale = rationale,
            ConfidenceLevel = confidence.ToString(),
            CreatedAt = DateTimeOffset.UtcNow,
        });
    }

    // GET /coach/students/{golferId}/rounds/{roundId}/reflections
    //
    // 404 unless reflections are shared.
    [HttpGet("rounds/{roundId:guid}/reflections", Name = "listStudentReflections")]
    [ProducesResponseType<List<ReflectionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<ReflectionResponse>>> Reflections(Guid golferId, Guid roundId, CancellationToken ct)
    {
        if (await LinkAsync(golferId, CoachShare.Reflections, ct) is null) return NotFound();
        if (!await _db.OwnsRoundAsync(roundId, golferId, ct)) return NotFound();

        var items = await _db.Reflections.AsNoTracking()
            .Where(r => r.RoundId == roundId && r.UserId == golferId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
        return Ok(items.Select(ReflectionsController.ToResponse).ToList());
    }

    // GET /coach/students/{golferId}/practice-sessions?skip=0&take=20
    //
    // As GET /me/practice-sessions; 404 unless practice is shared, and the notes only when
    // reflections are shared too.
    [HttpGet("practice-sessions", Name = "listStudentPracticeSessions")]
    [ProducesResponseType<List<PracticeSessionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ResponseHeader(StatusCodes.Status200OK, RoundsController.TotalCountHeader, "integer", "Total number of the student's practice sessions.")]
    public async Task<ActionResult<List<PracticeSessionResponse>>> PracticeSessions(
        Guid golferId, [FromQuery] int skip = 0, [FromQuery] int take = 20, CancellationToken ct = default)
    {
        if (await LinkAsync(golferId, CoachShare.Practice, ct) is not { } link) return NotFound();

        var (sessions, total) = await PracticeSessionsController.PageAsync(
            _db, golferId, skip, take, CoachAccess.Covers(link, CoachShare.Reflections), ct);
        Response.Headers[RoundsController.TotalCountHeader] = total.ToString();
        return Ok(sessions);
    }

    // GET /coach/students/{golferId}/practice-sessions/{sessionId}
    [HttpGet("practice-sessions/{sessionId:guid}", Name = "getStudentPracticeSession")]
    [ProducesResponseType<PracticeSessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PracticeSessionResponse>> PracticeSession(
        Guid golferId, Guid sessionId, CancellationToken ct)
    {
        if (await LinkAsync(golferId, CoachShare.Practice, ct) is not { } link) return NotFound();

        var session = await _db.PracticeSessions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.PracticeSessionId == sessionId && s.UserId == golferId, ct);
        return session is null
            ? NotFound()
            : Ok(PracticeSessionsController.ToResponse(session, CoachAccess.Covers(link, CoachShare.Reflections)));
    }

    // GET /coach/students/{golferId}/timeline?take=20&from=2026-01-01&to=2026-12-31
    [HttpGet("timeline", Name = "getStudentTimeline")]
    [ProducesResponseType<RoundTimelineResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoundTimelineResponse>> Timeline(
        Guid golferId,
        [FromQuery] int take = 20,
        [FromQuery] string? from = null,
        [FromQuery] string? to = null,
        CancellationToken ct = default)
    {
        if (await LinkAsync(golferId, CoachShare.RoundsAndProgress, ct) is null) return NotFound();

        DateOnly? fromDate = null;
        DateOnly? toDate = null;
        if (!string.IsNullOrWhiteSpace(from))
        {
            if (!IsoDate.TryParse(from, out var f)) return this.FieldError(nameof(from), "from must be an ISO date, yyyy-MM-dd.");
            fromDate = f;
        }
        if (!string.IsNullOrWhiteSpace(to))
        {
            if (!IsoDate.TryParse(to, out var t)) return this.FieldError(nameof(to), "to must be an ISO date, yyyy-MM-dd.");
            toDate = t;
        }

        return Ok(await _timeline.GetRoundTimelineAsync(golferId, take, fromDate, toDate, ct));
    }

    // GET /coach/students/{golferId}/timeline/monthly?months=6
    [HttpGet("timeline/monthly", Name = "getStudentMonthlyTimeline")]
    [ProducesResponseType<MonthlyTimelineResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MonthlyTimelineResponse>> MonthlyTimeline(
        Guid golferId, [FromQuery] int months = 6, CancellationToken ct = default)
    {
        if (await LinkAsync(golferId, CoachShare.RoundsAndProgress, ct) is null) return NotFound();

        return Ok(await _timeline.GetMonthlyTimelineAsync(golferId, months, ct));
    }

    // GET /coach/students/{golferId}/monthly-reviews/{monthStart}
    [HttpGet("monthly-reviews/{monthStart}", Name = "getStudentMonthlyReview")]
    [ProducesResponseType<MonthlyReviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MonthlyReviewResponse>> MonthlyReview(
        Guid golferId, string monthStart, CancellationToken ct)
    {
        if (await LinkAsync(golferId, CoachShare.RoundsAndProgress, ct) is null) return NotFound();
        if (!IsoDate.TryParse(monthStart, out var m))
            return this.FieldError(nameof(monthStart), "monthStart must be an ISO date, yyyy-MM-dd.");

        var review = await _db.MonthlyReviews.AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == golferId && r.MonthStart == m, ct);
        return review is null ? NotFound() : Ok(MonthlyReviewsController.ToResponse(review));
    }

    private Task<Domain.Entities.CoachLink?> LinkAsync(Guid golferId, CoachShare share, CancellationToken ct) =>
        _access.LinkCoveringAsync(User.GetUserId(), golferId, share, ct);
}
