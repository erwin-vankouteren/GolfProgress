using GolfProgress.Domain.Entities;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace GolfProgress.Api.Services;

public sealed class MonthlyReviewService
{
    private readonly GolfProgressDbContext _db;

    public MonthlyReviewService(GolfProgressDbContext db) => _db = db;

    public async Task<MonthlyReview> GenerateAsync(Guid userId, DateOnly monthStart, string? focusNextMonth, CancellationToken ct)
    {
        var nextMonth = monthStart.AddMonths(1);

        // rounds in month
        var rounds = await _db.Rounds.AsNoTracking()
            .Where(r => r.UserId == userId && r.PlayedAt >= monthStart && r.PlayedAt < nextMonth)
            .ToListAsync(ct);

        var roundIds = rounds.Select(r => r.RoundId).ToList();

        // fitness scores for those rounds
        var scores = await _db.FitnessScores.AsNoTracking()
            .Where(fs => fs.UserId == userId && roundIds.Contains(fs.RoundId))
            .ToListAsync(ct);

        decimal? avg = scores.Count == 0 ? null : (decimal)scores.Average(s => s.ScoreTotal);

        // previous month avg for change
        var prevStart = monthStart.AddMonths(-1);
        var prevEnd = monthStart;
        var prevRoundIds = await _db.Rounds.AsNoTracking()
            .Where(r => r.UserId == userId && r.PlayedAt >= prevStart && r.PlayedAt < prevEnd)
            .Select(r => r.RoundId)
            .ToListAsync(ct);

        var prevScores = await _db.FitnessScores.AsNoTracking()
            .Where(fs => fs.UserId == userId && prevRoundIds.Contains(fs.RoundId))
            .ToListAsync(ct);

        decimal? prevAvg = prevScores.Count == 0 ? null : (decimal)prevScores.Average(s => s.ScoreTotal);
        decimal? change = (avg.HasValue && prevAvg.HasValue) ? avg.Value - prevAvg.Value : null;

        // highlights: keep simple and honest
        var microWins = await _db.MicroWins.AsNoTracking()
            .Where(mw => mw.UserId == userId && roundIds.Contains(mw.RoundId))
            .ToListAsync(ct);

        var mostCommonCategory = microWins.Count == 0
            ? null
            : microWins.GroupBy(m => m.Category.ToString()).OrderByDescending(g => g.Count()).First().Key;

        var highlights = new List<string>
        {
            $"Rounds logged: {rounds.Count}",
            scores.Count == 0 ? "Fitness score: not enough data yet" : $"Average fitness score: {avg:0.##}",
        };

        if (!string.IsNullOrWhiteSpace(mostCommonCategory))
            highlights.Add($"Most common micro‑win theme: {mostCommonCategory}");

        if (change.HasValue)
            highlights.Add($"Change vs last month: {change.Value:+0.##;-0.##;0}");

        var now = DateTimeOffset.UtcNow;

        // Upsert MonthlyReview
        var existing = await _db.MonthlyReviews.FirstOrDefaultAsync(m => m.UserId == userId && m.MonthStart == monthStart, ct);
        if (existing is null)
        {
            existing = new MonthlyReview
            {
                MonthlyReviewId = Guid.NewGuid(),
                UserId = userId,
                MonthStart = monthStart
            };
            _db.MonthlyReviews.Add(existing);
        }

        existing.FitnessAvg = avg;
        existing.FitnessChange = change;
        existing.FocusNextMonth = focusNextMonth ?? existing.FocusNextMonth;

        existing.HighlightsJson = JsonDocument.Parse(
            JsonSerializer.Serialize(new { highlights }),
            new JsonDocumentOptions()
        );

        existing.ComputedAt = now;

        // Event
        _db.Events.Add(new EventLog
        {
            EventLogId = Guid.NewGuid(),
            UserId = userId,
            EventType = "MonthlyReviewGenerated",
            RelatedId = existing.MonthlyReviewId,
            PayloadJson = JsonDocument.Parse(
                JsonSerializer.Serialize(new { userId, monthStart = monthStart.ToString("yyyy-MM-dd") }),
                new JsonDocumentOptions()
            ),
            CreatedAt = now
        });

        await _db.SaveChangesAsync(ct);
        return existing;
    }
}