using GolfProgress.Api.Contracts.MonthlyReviews;
using GolfProgress.Api.Contracts;
using GolfProgress.Api.ErrorHandling;
using GolfProgress.Api.Security;
using GolfProgress.Api.Services;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

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

        var exists = await _db.Users.AnyAsync(u => u.UserId == userId, ct);
        if (!exists) return NotFound();

        var review = await _svc.GenerateAsync(userId, monthStart, request.FocusNextMonth, ct);
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
        var highlights = new List<string>();
        try
        {
            if (r.HighlightsJson != null)
            {
                var root = r.HighlightsJson.RootElement;
                if (root.TryGetProperty("highlights", out var arr) && arr.ValueKind == JsonValueKind.Array)
                    highlights = arr.EnumerateArray().Select(x => x.GetString() ?? "").Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            }
        }
        catch { /* keep empty list */ }

        return new MonthlyReviewResponse
        {
            MonthlyReviewId = r.MonthlyReviewId,
            UserId = r.UserId,
            MonthStart = r.MonthStart.ToString("yyyy-MM-dd"),
            FitnessAvg = r.FitnessAvg,
            FitnessChange = r.FitnessChange,
            FocusNextMonth = r.FocusNextMonth,
            Highlights = highlights,
            ComputedAt = r.ComputedAt
        };
    }
}