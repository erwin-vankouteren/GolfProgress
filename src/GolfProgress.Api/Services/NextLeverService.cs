using GolfProgress.Domain.Entities;
using GolfProgress.Api.ErrorHandling;
using GolfProgress.Api.Localization;
using GolfProgress.Domain.Enums;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace GolfProgress.Api.Services;

public sealed class NextLeverService
{
    private readonly GolfProgressDbContext _db;

    public NextLeverService(GolfProgressDbContext db) => _db = db;

    public async Task<(string leverKey, string title, string rec, string rationale, ConfidenceLevel confidence)> RecommendAsync(Guid roundId, Guid userId, CancellationToken ct)
    {
        var round = await _db.Rounds.AsNoTracking().FirstOrDefaultAsync(r => r.RoundId == roundId && r.UserId == userId, ct)
            ?? throw new NotFoundException($"Round {roundId} not found.");

        var baseline = await _db.UserBaselines.AsNoTracking().FirstOrDefaultAsync(b => b.UserId == round.UserId, ct);
        var confidence = baseline?.ConfidenceLevel ?? ConfidenceLevel.Low;

        // In the request's language (Accept-Language).
        var texts = ApiTexts.Current;
        (string key, string title, string rec, string rationale, ConfidenceLevel confidence) Answer(
            string key, LeverText lever, decimal? delta, ConfidenceLevel level)
        {
            var (title, rec) = texts.Lever(lever);
            return (key, title, rec, texts.LeverRationale(lever, delta), level);
        }

        // Each stat scores twice (higher = bigger opportunity): against the baseline, which
        // only counts getting worse, and by how many strokes it cost, as if there were no
        // baseline. The second decides when nothing got worse, as on a first round, whose
        // baseline is that round itself (#292).
        var candidates = new List<(string key, LeverText lever, decimal? delta, decimal score, decimal cost)>();
        void Add(string key, LeverText lever, decimal count, decimal? baseVal, decimal weight, decimal costWeight)
        {
            decimal? delta = baseVal is decimal b ? count - b : null;
            var score = delta is decimal d ? Math.Max(d, 0m) * weight : count * costWeight;
            // A stat that improved is never the focus, however many strokes it still cost.
            var cost = delta < 0m ? 0m : count * costWeight;
            candidates.Add((key, lever, delta, score, cost));
        }

        // Counts are scaled to 18 holes before use, both to compare against the
        // baseline (which holds 18-hole equivalents) and so the weights below mean
        // the same thing for a 9-hole round (#35). Lower is better for all three.
        if (HoleScaling.Per18(round.Penalties, round) is decimal penalties)
            Add("penalties", LeverText.Penalties, penalties, baseline?.AvgPenalties, 3m, 1m);
        if (HoleScaling.Per18(round.ThreePutts, round) is decimal threePutts)
            Add("three_putts", LeverText.ThreePutts, threePutts, baseline?.AvgThreePutts, 4m, 2m);
        if (HoleScaling.Per18(round.BlowUpHoles, round) is decimal blowUps)
            Add("damage_control", LeverText.DamageControl, blowUps, baseline?.AvgBlowUpHoles, 2m, 1.5m);

        // If no stats at all
        if (candidates.Count == 0)
            return Answer("data_quality", LeverText.DataQuality, null, ConfidenceLevel.Low);

        var best = candidates.OrderByDescending(c => c.score).ThenByDescending(c => c.cost).First();
        if (best.score > 0m)
            return Answer(best.key, best.lever, best.delta, confidence);

        // Nothing got worse: the stat that cost the most strokes without improving, explained
        // without a delta of 0. If every stat improved or was zero, keep doing what worked.
        if (best.cost > 0m)
            return Answer(best.key, best.lever, null, confidence);
        return Answer("hold_steady", LeverText.HoldSteady, baseline is null ? null : 0m, confidence);
    }

    public async Task LogEventAsync(Guid userId, Guid roundId, string leverKey, CancellationToken ct)
    {
        _db.Events.Add(new EventLog
        {
            EventLogId = Guid.NewGuid(),
            UserId = userId,
            EventType = "NextLeverRecommended",
            RelatedId = roundId,
            PayloadJson = JsonDocument.Parse(
                JsonSerializer.Serialize(new { roundId, leverKey }),
                new JsonDocumentOptions()
            ),
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(ct);
    }
}