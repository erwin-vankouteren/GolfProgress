using GolfProgress.Api.Localization;
using GolfProgress.Domain.Entities;
using GolfProgress.Domain.Enums;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace GolfProgress.Api.Services;

public sealed class MonthlyReviewService
{
    private readonly GolfProgressDbContext _db;

    public MonthlyReviewService(GolfProgressDbContext db) => _db = db;

    /// <summary>
    /// A review's facts as data (#162), read leniently: reviews generated before they
    /// were stored, or anything unexpected, read as null.
    /// </summary>
    public static (int? RoundsLogged, string? TopMicroWinCategory) ReadFacts(JsonDocument? highlightsJson)
    {
        if (highlightsJson is null || highlightsJson.RootElement.ValueKind != JsonValueKind.Object)
            return (null, null);

        var root = highlightsJson.RootElement;
        int? rounds = root.TryGetProperty("roundsLogged", out var r) && r.ValueKind == JsonValueKind.Number
            && r.TryGetInt32(out var count) && count >= 0 ? count : null;
        string? category = root.TryGetProperty("topMicroWinCategory", out var c) && c.ValueKind == JsonValueKind.String
            ? c.GetString() : null;

        return (rounds, category);
    }

    /// <summary>A review's highlights, read leniently: anything unexpected reads as none.</summary>
    public static List<string> ReadHighlights(JsonDocument? highlightsJson)
    {
        if (highlightsJson is null
            || highlightsJson.RootElement.ValueKind != JsonValueKind.Object
            || !highlightsJson.RootElement.TryGetProperty("highlights", out var arr)
            || arr.ValueKind != JsonValueKind.Array)
            return new();

        return arr.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString()!)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }

    /// <summary>
    /// A review's highlights in <paramref name="texts"/>' language, written from its
    /// facts. Reviews generated before the facts were stored (#162) only have their
    /// English sentences, which are shown as they are.
    /// </summary>
    public static List<string> Highlights(MonthlyReview review, ApiTexts texts)
    {
        var (roundsLogged, topCategory) = ReadFacts(review.HighlightsJson);
        return roundsLogged is int rounds
            ? HighlightsFor(texts, rounds, review.FitnessAvg, topCategory, review.FitnessChange)
            : ReadHighlights(review.HighlightsJson);
    }

    private static List<string> HighlightsFor(ApiTexts texts, int roundsLogged, decimal? average, string? topCategory, decimal? change)
    {
        var highlights = new List<string>
        {
            texts.RoundsLogged(roundsLogged),
            texts.AverageFitnessScore(average),
        };

        if (Enum.TryParse<MicroWinCategory>(topCategory, out var category))
            highlights.Add(texts.TopMicroWinTheme(category));

        if (change.HasValue)
            highlights.Add(texts.ChangeVsLastMonth(change.Value));

        return highlights;
    }

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

        // Ties go to the category named first, so generating twice gives the same theme.
        var mostCommonCategory = microWins.Count == 0
            ? null
            : microWins.GroupBy(m => m.Category.ToString())
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key, StringComparer.Ordinal)
                .First().Key;

        // Stored in English; Highlights shows them in the reader's language.
        var highlights = HighlightsFor(ApiTexts.English, rounds.Count, avg, mostCommonCategory, change);

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

        // The sentences for display, and the same facts as data clients can localise
        // (#162), stored together so both describe the month as it was when generated.
        existing.HighlightsJson = JsonDocument.Parse(
            JsonSerializer.Serialize(new
            {
                highlights,
                roundsLogged = rounds.Count,
                topMicroWinCategory = mostCommonCategory
            }),
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