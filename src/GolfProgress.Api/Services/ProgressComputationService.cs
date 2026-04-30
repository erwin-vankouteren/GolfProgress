using GolfProgress.Domain.Entities;
using GolfProgress.Domain.Enums;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace GolfProgress.Api.Services;

public sealed class ProgressComputationService
{
    private readonly GolfProgressDbContext _db;

    public ProgressComputationService(GolfProgressDbContext db) => _db = db;

    public async Task<(UserBaseline baseline, FitnessScore fitness)> ComputeForRoundAsync(Guid roundId, short windowN, CancellationToken ct)
    {
        var round = await _db.Rounds.FirstOrDefaultAsync(r => r.RoundId == roundId, ct)
            ?? throw new InvalidOperationException($"Round {roundId} not found.");

        // Baseline rounds: last N rounds BEFORE this round (date-based)
        var priorRounds = await _db.Rounds.AsNoTracking()
            .Where(r => r.UserId == round.UserId && (r.PlayedAt < round.PlayedAt || (r.PlayedAt == round.PlayedAt && r.RoundId != round.RoundId)))
            .OrderByDescending(r => r.PlayedAt)
            .Take(windowN)
            .ToListAsync(ct);

        var completeness = ComputeCompleteness(priorRounds);
        var confidence = ConfidenceFrom(priorRounds.Count, completeness);

        var baseline = await UpsertBaselineAsync(round.UserId, windowN, priorRounds, completeness, confidence, ct);

        // Compute FitnessScore vs baseline (simple v1)
        var fitness = await UpsertFitnessScoreAsync(round, baseline, confidence, ct);

        // Emit events (append-only)
        var now = DateTimeOffset.UtcNow;
        _db.Events.Add(new EventLog
        {
            EventLogId = Guid.NewGuid(),
            UserId = round.UserId,
            EventType = "BaselineComputed",
            RelatedId = roundId,
            PayloadJson = JsonDocument.Parse("{\"windowRounds\":" + windowN + "}", new JsonDocumentOptions()),
            CreatedAt = now
        });

        _db.Events.Add(new EventLog
        {
            EventLogId = Guid.NewGuid(),
            UserId = round.UserId,
            EventType = "FitnessScoreComputed",
            RelatedId = roundId,
            PayloadJson = JsonDocument.Parse("{\"scoreTotal\":" + fitness.ScoreTotal + "}", new JsonDocumentOptions()),
            CreatedAt = now
        });

        await _db.SaveChangesAsync(ct);

        return (baseline, fitness);
    }

    private static decimal ComputeCompleteness(List<Round> rounds)
    {
        if (rounds.Count == 0) return 0m;

        // metrics we care about for baseline quality
        int totalPossible = rounds.Count * 6; // putts, penalties, fairways, gir, three_putts, blow_up_holes
        int available = 0;

        foreach (var r in rounds)
        {
            if (r.TotalPutts != null) available++;
            if (r.Penalties != null) available++;
            if (r.FairwaysHit != null) available++;
            if (r.Gir != null) available++;
            if (r.ThreePutts != null) available++;
            if (r.BlowUpHoles != null) available++;
        }

        return totalPossible == 0 ? 0m : (decimal)available / totalPossible;
    }

    private static ConfidenceLevel ConfidenceFrom(int sampleCount, decimal completenessRatio)
    {
        if (sampleCount >= 8 && completenessRatio >= 0.5m) return ConfidenceLevel.High;
        if (sampleCount >= 4) return ConfidenceLevel.Medium;
        return ConfidenceLevel.Low;
    }

    private async Task<UserBaseline> UpsertBaselineAsync(Guid userId, short windowN, List<Round> rounds, decimal completeness, ConfidenceLevel confidence, CancellationToken ct)
    {
        decimal? avgScore = rounds.Count == 0 ? null : (decimal)rounds.Average(r => r.TotalStrokes);
        decimal? std = rounds.Count < 2 ? null : StdDev(rounds.Select(r => (decimal)r.TotalStrokes));

        decimal? avgPutts = AvgNullable(rounds.Select(r => r.TotalPutts));
        decimal? avgPen = AvgNullable(rounds.Select(r => r.Penalties));
        decimal? avg3p = AvgNullable(rounds.Select(r => r.ThreePutts));
        decimal? avgBlow = AvgNullable(rounds.Select(r => r.BlowUpHoles));
        decimal? avgFair = AvgNullable(rounds.Select(r => r.FairwaysHit));
        decimal? avgGir = AvgNullable(rounds.Select(r => r.Gir));

        var existing = await _db.UserBaselines.FirstOrDefaultAsync(b => b.UserId == userId, ct);

        var now = DateTimeOffset.UtcNow;

        if (existing is null)
        {
            existing = new UserBaseline
            {
                UserId = userId,
                WindowRounds = windowN
            };
            _db.UserBaselines.Add(existing);
        }

        existing.WindowRounds = windowN;
        existing.AvgScore = avgScore;
        existing.ScoreStdDev = std;

        existing.AvgPutts = avgPutts;
        existing.AvgPenalties = avgPen;
        existing.AvgThreePutts = avg3p;
        existing.AvgBlowUpHoles = avgBlow;
        existing.AvgFairwaysHit = avgFair;
        existing.AvgGir = avgGir;

        existing.CompletenessRatio = completeness;
        existing.ConfidenceLevel = confidence;
        existing.ComputedAt = now;

        await _db.SaveChangesAsync(ct);
        return existing;
    }

    private async Task<FitnessScore> UpsertFitnessScoreAsync(Round round, UserBaseline baseline, ConfidenceLevel confidence, CancellationToken ct)
    {
        var existing = await _db.FitnessScores.FirstOrDefaultAsync(fs => fs.RoundId == round.RoundId, ct);

        var (execution, damage, consistency, habits) = ComputePillars(round, baseline);

        var total = (short)Math.Clamp(execution + damage + consistency + habits, 0, 100);

        var now = DateTimeOffset.UtcNow;

        if (existing is null)
        {
            existing = new FitnessScore
            {
                FitnessScoreId = Guid.NewGuid(),
                UserId = round.UserId,
                RoundId = round.RoundId
            };
            _db.FitnessScores.Add(existing);
        }

        existing.Execution = execution;
        existing.DamageControl = damage;
        existing.Consistency = consistency;
        existing.Habits = habits;
        existing.ScoreTotal = total;
        existing.ConfidenceLevel = confidence;
        existing.ComputedAt = now;

        // Keep explanation minimal for now
        existing.ExplanationJson = JsonDocument.Parse(
            JsonSerializer.Serialize(new
            {
                windowRounds = baseline.WindowRounds,
                avgScore = baseline.AvgScore,
                scoreStdDev = baseline.ScoreStdDev,
                pillars = new { execution, damage, consistency, habits }
            }),
            new JsonDocumentOptions()
        );

        await _db.SaveChangesAsync(ct);
        return existing;
    }

    private static (short execution, short damage, short consistency, short habits) ComputePillars(Round r, UserBaseline b)
    {
        // Very simple v1 scoring (0..25 each). Improve later.
        // Execution: better score than baseline => higher
        short execution = 12;
        if (b.AvgScore != null)
        {
            var diff = (decimal)r.TotalStrokes - b.AvgScore.Value; // negative is good
            execution = (short)Math.Clamp(12 - (int)Math.Round(diff), 0, 25);
        }

        // Damage control: penalties + blow-up holes
        short damage = 12;
        int pen = r.Penalties ?? 0;
        int blow = r.BlowUpHoles ?? 0;
        damage = (short)Math.Clamp(25 - (pen * 4) - (blow * 2), 0, 25);

        // Consistency: reward staying within 1 std dev of baseline
        short consistency = 12;
        if (b.ScoreStdDev != null && b.AvgScore != null && b.ScoreStdDev.Value > 0)
        {
            var within = Math.Abs((decimal)r.TotalStrokes - b.AvgScore.Value) <= b.ScoreStdDev.Value;
            consistency = (short)(within ? 18 : 8);
        }

        // Habits: placeholder v1 (we’ll make this based on frequency later)
        short habits = 10;

        return (execution, damage, consistency, habits);
    }

    private static decimal? AvgNullable(IEnumerable<short?> values)
    {
        var list = values.Where(v => v != null).Select(v => (decimal)v!.Value).ToList();
        if (list.Count == 0) return null;
        return list.Average();
    }

    private static decimal StdDev(IEnumerable<decimal> values)
    {
        var list = values.ToList();
        if (list.Count < 2) return 0m;
        var avg = list.Average();
        var variance = list.Sum(v => (v - avg) * (v - avg)) / (list.Count - 1);
        return (decimal)Math.Sqrt((double)variance);
    }
}
