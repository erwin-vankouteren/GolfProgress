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
        (string key, string title, string rec, string rationale, decimal score) Candidate(
            string key, LeverText lever, decimal? delta, decimal score)
        {
            var (title, rec) = texts.Lever(lever);
            return (key, title, rec, texts.LeverRationale(lever, delta), score);
        }

        // Build "opportunity" candidates (higher = bigger opportunity)
        var candidates = new List<(string key, string title, string rec, string rationale, decimal score)>();

        // Counts are scaled to 18 holes before use, both to compare against the
        // baseline (which holds 18-hole equivalents) and so the weights below mean
        // the same thing for a 9-hole round (#35).

        // Penalties (lower is better)
        if (HoleScaling.Per18(round.Penalties, round) is decimal penalties)
        {
            var baseVal = baseline?.AvgPenalties;
            var delta = baseVal.HasValue ? penalties - baseVal.Value : 0m;
            var score = baseVal.HasValue ? Math.Max(delta, 0m) * 3m : penalties * 1m;

            candidates.Add(Candidate("penalties", LeverText.Penalties, baseVal.HasValue ? delta : null, score));
        }

        // Three-putts
        if (HoleScaling.Per18(round.ThreePutts, round) is decimal threePutts)
        {
            var baseVal = baseline?.AvgThreePutts;
            var delta = baseVal.HasValue ? threePutts - baseVal.Value : 0m;
            var score = baseVal.HasValue ? Math.Max(delta, 0m) * 4m : threePutts * 2m;

            candidates.Add(Candidate("three_putts", LeverText.ThreePutts, baseVal.HasValue ? delta : null, score));
        }

        // Blow-up holes
        if (HoleScaling.Per18(round.BlowUpHoles, round) is decimal blowUps)
        {
            var baseVal = baseline?.AvgBlowUpHoles;
            var delta = baseVal.HasValue ? blowUps - baseVal.Value : 0m;
            var score = baseVal.HasValue ? Math.Max(delta, 0m) * 2m : blowUps * 1.5m;

            candidates.Add(Candidate("damage_control", LeverText.DamageControl, baseVal.HasValue ? delta : null, score));
        }

        // If no stats at all
        if (candidates.Count == 0)
        {
            var dataQuality = Candidate("data_quality", LeverText.DataQuality, null, 0m);
            return (dataQuality.key, dataQuality.title, dataQuality.rec, dataQuality.rationale, ConfidenceLevel.Low);
        }

        var best = candidates.OrderByDescending(c => c.score).First();
        return (best.key, best.title, best.rec, best.rationale, confidence);
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