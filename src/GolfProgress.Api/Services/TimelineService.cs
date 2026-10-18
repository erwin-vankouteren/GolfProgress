using GolfProgress.Api.Contracts;
using GolfProgress.Api.Localization;
using GolfProgress.Api.Contracts.Timeline;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GolfProgress.Api.Services;

public sealed class TimelineService
{
    private readonly GolfProgressDbContext _db;

    public TimelineService(GolfProgressDbContext db) => _db = db;

    public async Task<RoundTimelineResponse> GetRoundTimelineAsync(Guid userId, int take, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        take = Math.Clamp(take, 1, 100);

        var query = _db.Rounds.AsNoTracking().Where(r => r.UserId == userId);

        if (from.HasValue) query = query.Where(r => r.PlayedAt >= from.Value);
        if (to.HasValue) query = query.Where(r => r.PlayedAt <= to.Value);

        var rounds = await query
            .OrderByDescending(r => r.PlayedAt)
            .ThenByDescending(r => r.CreatedAt)
            .Take(take)
            .Select(r => new
            {
                r.RoundId,
                r.PlayedAt,
                r.CreatedAt,
                r.Holes,
                r.TotalStrokes,
                r.TotalPutts,
                r.Penalties,
                r.ThreePutts,
                r.BlowUpHoles
            })
            .ToListAsync(ct);

        var roundIds = rounds.Select(r => r.RoundId).ToList();

        // fitness scores (optional)
        var fitness = await _db.FitnessScores.AsNoTracking()
            .Where(fs => fs.UserId == userId && roundIds.Contains(fs.RoundId))
            .Select(fs => new { fs.RoundId, fs.ScoreTotal, Confidence = fs.ConfidenceLevel.ToString() })
            .ToListAsync(ct);

        var fitnessMap = fitness.ToDictionary(x => x.RoundId, x => x);

        // micro win title (optional) - take the earliest-created win for stable display
        var microWins = await _db.MicroWins.AsNoTracking()
            .Where(mw => mw.UserId == userId && roundIds.Contains(mw.RoundId))
            .OrderBy(mw => mw.CreatedAt)
            .ToListAsync(ct);

        // In the request's language, as everywhere micro-wins are shown.
        var texts = ApiTexts.Current;
        var microWinMap = microWins
            .GroupBy(x => x.RoundId)
            .ToDictionary(g => g.Key, g => MicroWinTexts.For(g.First(), texts).Title);

        // Compose response in chronological order for charting (oldest -> newest). Two
        // rounds on one day keep the order they were logged in.
        var items = rounds
            .OrderBy(r => r.PlayedAt)
            .ThenBy(r => r.CreatedAt)
            .Select(r =>
            {
                fitnessMap.TryGetValue(r.RoundId, out var fs);
                microWinMap.TryGetValue(r.RoundId, out var title);

                return new RoundTimelinePointResponse
                {
                    RoundId = r.RoundId,
                    PlayedAt = r.PlayedAt.ToString("yyyy-MM-dd"),
                    Holes = r.Holes,
                    TotalStrokes = r.TotalStrokes,
                    TotalPutts = r.TotalPutts,
                    Penalties = r.Penalties,
                    ThreePutts = r.ThreePutts,
                    BlowUpHoles = r.BlowUpHoles,
                    FitnessScoreTotal = fs?.ScoreTotal,
                    FitnessConfidence = fs?.Confidence,
                    MicroWinTitle = title
                };
            })
            .ToList();

        return new RoundTimelineResponse
        {
            UserId = userId,
            Count = items.Count,
            Items = items
        };
    }

    /// <summary>
    /// One point per calendar month: the last <paramref name="months"/> months up to and
    /// including this one (UTC), oldest first (#107). The averages come from the rounds
    /// and their scores, so every month with rounds has one whether or not a monthly
    /// review was generated for it; a month without scored rounds has none. Highlights
    /// come from the month's review, when there is one.
    /// </summary>
    public async Task<MonthlyTimelineResponse> GetMonthlyTimelineAsync(Guid userId, int months, CancellationToken ct)
    {
        months = Math.Clamp(months, 1, 24);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var thisMonth = new DateOnly(today.Year, today.Month, 1);
        var firstMonth = thisMonth.AddMonths(1 - months);
        var end = thisMonth.AddMonths(1);
        // The month before the first is read too, for the first month's change.
        var readFrom = firstMonth.AddMonths(-1);

        var rounds = await _db.Rounds.AsNoTracking()
            .Where(r => r.UserId == userId && r.PlayedAt >= readFrom && r.PlayedAt < end)
            .Select(r => new
            {
                r.PlayedAt,
                Score = _db.FitnessScores
                    .Where(fs => fs.RoundId == r.RoundId)
                    .Select(fs => (short?)fs.ScoreTotal)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        var byMonth = rounds
            .GroupBy(r => new DateOnly(r.PlayedAt.Year, r.PlayedAt.Month, 1))
            .ToDictionary(g => g.Key, g => g.ToList());

        var reviews = await _db.MonthlyReviews.AsNoTracking()
            .Where(r => r.UserId == userId && r.MonthStart >= firstMonth && r.MonthStart < end)
            .ToListAsync(ct);
        var reviewByMonth = reviews
            .GroupBy(r => r.MonthStart)
            .ToDictionary(g => g.Key, g => g.First());

        decimal? AverageIn(DateOnly month)
        {
            if (!byMonth.TryGetValue(month, out var inMonth)) return null;
            var scores = inMonth.Where(r => r.Score.HasValue).Select(r => (decimal)r.Score!.Value).ToList();
            return scores.Count == 0 ? null : Math.Round(scores.Average(), 2);
        }

        var items = new List<MonthlyTimelinePointResponse>();
        for (var month = firstMonth; month < end; month = month.AddMonths(1))
        {
            var avg = AverageIn(month);
            var previous = AverageIn(month.AddMonths(-1));
            reviewByMonth.TryGetValue(month, out var review);

            items.Add(new MonthlyTimelinePointResponse
            {
                MonthStart = month.ToString(IsoDate.Format),
                FitnessAvg = avg,
                FitnessChange = avg.HasValue && previous.HasValue ? avg.Value - previous.Value : null,
                RoundsLogged = byMonth.TryGetValue(month, out var inMonth) ? inMonth.Count : 0,
                Highlights = review is null ? new() : MonthlyReviewService.Highlights(review, ApiTexts.Current),
                HasReview = review is not null
            });
        }

        return new MonthlyTimelineResponse
        {
            UserId = userId,
            Months = months,
            Items = items
        };
    }
}
