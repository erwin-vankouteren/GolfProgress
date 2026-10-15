using GolfProgress.Domain.Entities;
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
            ?? throw new InvalidOperationException($"Round {roundId} not found.");

        var baseline = await _db.UserBaselines.AsNoTracking().FirstOrDefaultAsync(b => b.UserId == round.UserId, ct);
        var confidence = baseline?.ConfidenceLevel ?? ConfidenceLevel.Low;

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

            candidates.Add((
                "penalties",
                "Reduce penalty strokes",
                "Next round: choose the safest target when trouble is possible — one conservative decision can remove a penalty.",
                baseVal.HasValue ? $"Penalties vs baseline Δ={delta:0.##}" : "Penalties are a high-impact lever.",
                score
            ));
        }

        // Three-putts
        if (HoleScaling.Per18(round.ThreePutts, round) is decimal threePutts)
        {
            var baseVal = baseline?.AvgThreePutts;
            var delta = baseVal.HasValue ? threePutts - baseVal.Value : 0m;
            var score = baseVal.HasValue ? Math.Max(delta, 0m) * 4m : threePutts * 2m;

            candidates.Add((
                "three_putts",
                "Cut down 3‑putts",
                "Next round: on long putts, aim to finish inside a 1–2 meter circle — pace first, line second.",
                baseVal.HasValue ? $"3‑putts vs baseline Δ={delta:0.##}" : "Three‑putts are a quick scoring lever.",
                score
            ));
        }

        // Blow-up holes
        if (HoleScaling.Per18(round.BlowUpHoles, round) is decimal blowUps)
        {
            var baseVal = baseline?.AvgBlowUpHoles;
            var delta = baseVal.HasValue ? blowUps - baseVal.Value : 0m;
            var score = baseVal.HasValue ? Math.Max(delta, 0m) * 2m : blowUps * 1.5m;

            candidates.Add((
                "damage_control",
                "Improve damage control",
                "Next round: after a mistake, play the next shot for position — your goal is bogey, not hero.",
                baseVal.HasValue ? $"Blow‑ups vs baseline Δ={delta:0.##}" : "Reducing big holes improves scoring fastest.",
                score
            ));
        }

        // If no stats at all
        if (candidates.Count == 0)
        {
            return (
                "data_quality",
                "Add one stat for sharper insights",
                "Next round: add just one stat (penalties or putts) — you’ll unlock more precise progress insights.",
                "This round has minimal stats; one extra stat improves recommendation accuracy.",
                ConfidenceLevel.Low
            );
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