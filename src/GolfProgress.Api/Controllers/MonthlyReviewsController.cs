using GolfProgress.Api.Contracts.MonthlyReviews;
using GolfProgress.Api.Contracts;
using GolfProgress.Api.ErrorHandling;
using GolfProgress.Api.Security;
using GolfProgress.Api.Services;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GolfProgress.Api.Controllers;

[Authorize]
[ApiController]
public sealed class MonthlyReviewsController : ControllerBase
{
    private readonly MonthlyReviewService _svc;
    private readonly GolfProgressDbContext _db;

    public MonthlyReviewsController(MonthlyReviewService svc, GolfProgressDbContext db)
    {
        _svc = svc;
        _db = db;
    }


    // POST /me/monthly-reviews/generate
    [HttpPost("/me/monthly-reviews/generate", Name = "generateMonthlyReview")]
    [ProducesResponseType<MonthlyReviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MonthlyReviewResponse>> Generate([FromBody] GenerateMonthlyReviewRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!IsoDate.TryParse(request.MonthStart, out var monthStart))
            return this.FieldError(nameof(request.MonthStart), "MonthStart must be an ISO date, yyyy-MM-dd (the first day of the month).");

        // A review covers a calendar month and is read back by its first day, so a
        // review stored under "2026-04-15" could never be found again (#107).
        if (monthStart.Day != 1)
            return this.FieldError(nameof(request.MonthStart), "MonthStart must be the first day of a month, e.g. 2026-04-01.");

        var exists = await _db.Users.AnyAsync(u => u.UserId == userId, ct);
        if (!exists) return NotFound();

        var focus = string.IsNullOrWhiteSpace(request.FocusNextMonth) ? null : request.FocusNextMonth.Trim();
        if (focus is not null && focus.EnumerateRunes().Count() > GenerateMonthlyReviewRequest.FocusNextMonthMaxLength)
            return this.FieldError(nameof(request.FocusNextMonth),
                $"FocusNextMonth must be at most {GenerateMonthlyReviewRequest.FocusNextMonthMaxLength} characters.");

        var review = await _svc.GenerateAsync(userId, monthStart, focus, ct);
        return Ok(ToResponse(review));
    }

    // GET /me/monthly-reviews/{monthStart}
    [HttpGet("/me/monthly-reviews/{monthStart}", Name = "getMonthlyReview")]
    [ProducesResponseType<MonthlyReviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MonthlyReviewResponse>> Get(string monthStart, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!IsoDate.TryParse(monthStart, out var m))
            return this.FieldError(nameof(monthStart), "monthStart must be an ISO date, yyyy-MM-dd.");

        var review = await _db.MonthlyReviews.AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == userId && r.MonthStart == m, ct);

        if (review is null) return NotFound();

        return Ok(ToResponse(review));
    }

    private static MonthlyReviewResponse ToResponse(GolfProgress.Domain.Entities.MonthlyReview r)
    {
        var highlights = MonthlyReviewService.ReadHighlights(r.HighlightsJson);
        var (roundsLogged, topCategory) = MonthlyReviewService.ReadFacts(r.HighlightsJson);

        return new MonthlyReviewResponse
        {
            MonthlyReviewId = r.MonthlyReviewId,
            UserId = r.UserId,
            MonthStart = r.MonthStart.ToString("yyyy-MM-dd"),
            FitnessAvg = r.FitnessAvg,
            FitnessChange = r.FitnessChange,
            FocusNextMonth = r.FocusNextMonth,
            RoundsLogged = roundsLogged,
            TopMicroWinCategory = topCategory,
            Highlights = highlights,
            ComputedAt = r.ComputedAt
        };
    }
}