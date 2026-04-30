using GolfProgress.Api.Contracts.Timeline;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

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
        var microWinTitles = await _db.MicroWins.AsNoTracking()
            .Where(mw => mw.UserId == userId && roundIds.Contains(mw.RoundId))
            .OrderBy(mw => mw.CreatedAt)
            .Select(mw => new { mw.RoundId, mw.Title })
            .ToListAsync(ct);

        var microWinMap = microWinTitles
            .GroupBy(x => x.RoundId)
            .ToDictionary(g => g.Key, g => g.First().Title);

        // Compose response in chronological order for charting (oldest -> newest)
        var items = rounds
            .OrderBy(r => r.PlayedAt)
            .Select(r =>
            {
                fitnessMap.TryGetValue(r.RoundId, out var fs);
                microWinMap.TryGetValue(r.RoundId, out var title);

                return new RoundTimelinePointResponse
                {
                    RoundId = r.RoundId,
                    PlayedAt = r.PlayedAt.ToString("yyyy-MM-dd"),
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

    public async Task<MonthlyTimelineResponse> GetMonthlyTimelineAsync(Guid userId, int months, CancellationToken ct)
    {
        months = Math.Clamp(months, 1, 24);

        // Prefer MonthlyReviews when they exist (generated in step 3.5)
        var reviews = await _db.MonthlyReviews.AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.MonthStart)
            .Take(months)
            .ToListAsync(ct);

        var items = new List<MonthlyTimelinePointResponse>();

        foreach (var r in reviews.OrderBy(x => x.MonthStart))
        {
            var highlights = new List<string>();

            try
            {
                if (r.HighlightsJson != null)
                {
                    var root = r.HighlightsJson.RootElement;
                    if (root.TryGetProperty("highlights", out var arr) && arr.ValueKind == JsonValueKind.Array)
                    {
                        highlights = arr.EnumerateArray()
                            .Select(x => x.GetString() ?? "")
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .ToList();
                    }
                }
            }
            catch { /* keep empty */ }

            // rounds count (computed quickly)
            var nextMonth = r.MonthStart.AddMonths(1);
            var roundCount = await _db.Rounds.AsNoTracking()
                .CountAsync(x => x.UserId == userId && x.PlayedAt >= r.MonthStart && x.PlayedAt < nextMonth, ct);

            items.Add(new MonthlyTimelinePointResponse
            {
                MonthStart = r.MonthStart.ToString("yyyy-MM-dd"),
                FitnessAvg = r.FitnessAvg,
                FitnessChange = r.FitnessChange,
                RoundsLogged = roundCount,
                Highlights = highlights
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