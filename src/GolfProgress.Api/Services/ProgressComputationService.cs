using GolfProgress.Domain.Entities;
using GolfProgress.Domain.Enums;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace GolfProgress.Api.Services;

public sealed class ProgressComputationService
{
    /// <summary>Highest score any single pillar can contribute.</summary>
    internal const short PillarMax = 25;

    /// <summary>
    /// Score used when there is no evidence to judge a pillar on -- neither good nor
    /// bad. Deliberately below <see cref="PillarMax"/> so that absent data is never
    /// rewarded as if it were a clean result.
    /// </summary>
    internal const short PillarNeutral = 12;

    private readonly GolfProgressDbContext _db;

    public ProgressComputationService(GolfProgressDbContext db) => _db = db;

    public async Task<(UserBaseline baseline, FitnessScore fitness)> ComputeForRoundAsync(Guid roundId, Guid userId, short windowN, CancellationToken ct)
    {
        var round = await _db.Rounds.FirstOrDefaultAsync(r => r.RoundId == roundId && r.UserId == userId, ct)
            ?? throw new InvalidOperationException($"Round {roundId} not found.");

        // Two different baselines, for two different jobs.
        //
        // The SCORING baseline is the window of rounds played before this one --
        // the yardstick this round is judged against. It is never persisted,
        // because it belongs to this round rather than to the user.
        var priorRounds = await RecentRoundsAsync(round.UserId, windowN, before: round, ct);
        var scoringBaseline = BuildBaseline(round.UserId, windowN, priorRounds);

        var fitness = await UpsertFitnessScoreAsync(
            round, scoringBaseline, scoringBaseline.ConfidenceLevel, ct);

        // The CURRENT baseline is the user's most recent rounds, and is what
        // UserBaselines stores and GET /me/baseline serves. It is recomputed from
        // scratch on every call and does not depend on which round was just scored,
        // so recomputing an old round can no longer overwrite it with a stale
        // window. Any compute leaves it correct.
        var currentRounds = await RecentRoundsAsync(round.UserId, windowN, before: null, ct);
        var baseline = await UpsertCurrentBaselineAsync(
            BuildBaseline(round.UserId, windowN, currentRounds), ct);

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

    internal static decimal ComputeCompleteness(List<Round> rounds)
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

    internal static ConfidenceLevel ConfidenceFrom(int sampleCount, decimal completenessRatio)
    {
        if (sampleCount >= 8 && completenessRatio >= 0.5m) return ConfidenceLevel.High;
        if (sampleCount >= 4) return ConfidenceLevel.Medium;
        return ConfidenceLevel.Low;
    }

    /// <summary>
    /// The user's most recent <paramref name="windowN"/> rounds, newest first.
    /// When <paramref name="before"/> is supplied, only rounds played before it are
    /// considered -- same-day rounds count as prior, matching the original window.
    /// </summary>
    private Task<List<Round>> RecentRoundsAsync(
        Guid userId, short windowN, Round? before, CancellationToken ct)
    {
        var query = _db.Rounds.AsNoTracking().Where(r => r.UserId == userId);

        if (before is not null)
        {
            query = query.Where(r =>
                r.PlayedAt < before.PlayedAt ||
                (r.PlayedAt == before.PlayedAt && r.RoundId != before.RoundId));
        }

        return query
            .OrderByDescending(r => r.PlayedAt)
            // PlayedAt is a date, so rounds on the same day tie. Without a
            // tiebreaker the window was whatever order SQL Server happened to
            // return, which could differ between runs on identical data (#10).
            .ThenByDescending(r => r.CreatedAt)
            .Take(windowN)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Computes baseline statistics from a set of rounds. Pure: nothing is read
    /// from or written to the database, so the same rounds always give the same
    /// baseline.
    /// </summary>
    internal static UserBaseline BuildBaseline(Guid userId, short windowN, List<Round> rounds)
    {
        var completeness = ComputeCompleteness(rounds);

        return new UserBaseline
        {
            UserId = userId,
            WindowRounds = windowN,
            // Scores are averaged as 18-hole equivalents so a mix of 9- and 18-hole
            // rounds yields one coherent yardstick (#5).
            AvgScore = rounds.Count == 0 ? null : rounds.Average(StrokesPer18),
            ScoreStdDev = rounds.Count < 2 ? null : StdDev(rounds.Select(StrokesPer18)),
            AvgPutts = AvgNullable(rounds.Select(r => r.TotalPutts)),
            AvgPenalties = AvgNullable(rounds.Select(r => r.Penalties)),
            AvgThreePutts = AvgNullable(rounds.Select(r => r.ThreePutts)),
            AvgBlowUpHoles = AvgNullable(rounds.Select(r => r.BlowUpHoles)),
            AvgFairwaysHit = AvgNullable(rounds.Select(r => r.FairwaysHit)),
            AvgGir = AvgNullable(rounds.Select(r => r.Gir)),
            CompletenessRatio = completeness,
            ConfidenceLevel = ConfidenceFrom(rounds.Count, completeness),
            ComputedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>Writes the user's single current baseline row.</summary>
    private async Task<UserBaseline> UpsertCurrentBaselineAsync(
        UserBaseline computed, CancellationToken ct)
    {
        var existing = await _db.UserBaselines
            .FirstOrDefaultAsync(b => b.UserId == computed.UserId, ct);

        if (existing is null)
        {
            _db.UserBaselines.Add(computed);
            await _db.SaveChangesAsync(ct);
            return computed;
        }

        existing.WindowRounds = computed.WindowRounds;
        existing.AvgScore = computed.AvgScore;
        existing.ScoreStdDev = computed.ScoreStdDev;

        existing.AvgPutts = computed.AvgPutts;
        existing.AvgPenalties = computed.AvgPenalties;
        existing.AvgThreePutts = computed.AvgThreePutts;
        existing.AvgBlowUpHoles = computed.AvgBlowUpHoles;
        existing.AvgFairwaysHit = computed.AvgFairwaysHit;
        existing.AvgGir = computed.AvgGir;

        existing.CompletenessRatio = computed.CompletenessRatio;
        existing.ConfidenceLevel = computed.ConfidenceLevel;
        existing.ComputedAt = computed.ComputedAt;

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

    internal static (short execution, short damage, short consistency, short habits) ComputePillars(Round r, UserBaseline b)
    {
        // Very simple v1 scoring (0..PillarMax each). Improve later.
        //
        // The baseline is held in 18-hole equivalents, so the round is compared on
        // the same footing. Comparing a 9-hole total directly read as a ~45-stroke
        // improvement (#5).
        var strokes = StrokesPer18(r);

        // Execution: better score than baseline => higher
        short execution = PillarNeutral;
        if (b.AvgScore != null)
        {
            var diff = strokes - b.AvgScore.Value; // negative is good
            execution = (short)Math.Clamp(PillarNeutral - (int)Math.Round(diff), 0, PillarMax);
        }

        // Damage control: penalties + blow-up holes.
        //
        // A null metric means "not tracked", not "zero". Reading it as zero awarded
        // an untracked round the full 25, so tracking less scored better than
        // tracking honestly -- the opposite of what the confidence model elsewhere
        // in this service is for. Instead, each untracked metric lowers the
        // attainable ceiling toward the neutral midpoint, so a perfect damage score
        // requires actual evidence. Deductions themselves are unchanged.
        short damage = (short)Math.Clamp(
            DamageCeiling(r) - ((r.Penalties ?? 0) * 4) - ((r.BlowUpHoles ?? 0) * 2),
            0, PillarMax);

        // Consistency: reward staying within 1 std dev of baseline
        short consistency = PillarNeutral;
        if (b.ScoreStdDev != null && b.AvgScore != null && b.ScoreStdDev.Value > 0)
        {
            var within = Math.Abs(strokes - b.AvgScore.Value) <= b.ScoreStdDev.Value;
            consistency = (short)(within ? 18 : 8);
        }

        // Habits: placeholder v1 (we’ll make this based on frequency later)
        short habits = 10;

        return (execution, damage, consistency, habits);
    }

    /// <summary>
    /// The highest damage-control score a round can reach, given how many of its two
    /// damage metrics were actually tracked.
    ///
    /// With both tracked the full <see cref="PillarMax"/> is attainable. With neither,
    /// there is no evidence either way and the score is the neutral
    /// <see cref="PillarNeutral"/>. With one, the ceiling is the midpoint of those two
    /// -- so partial evidence earns a partial ceiling rather than full marks.
    /// </summary>
    internal static short DamageCeiling(Round r)
    {
        var tracked = (r.Penalties.HasValue ? 1 : 0) + (r.BlowUpHoles.HasValue ? 1 : 0);

        return tracked switch
        {
            2 => PillarMax,
            1 => (short)((PillarMax + PillarNeutral) / 2),
            _ => PillarNeutral
        };
    }

    /// <summary>
    /// The round's total scaled to 18 holes, so 9- and 18-hole rounds can be compared
    /// and averaged together. A 9-hole 45 counts as a 90.
    /// </summary>
    internal static decimal StrokesPer18(Round r) => r.TotalStrokes * 18m / r.Holes;

    internal static decimal? AvgNullable(IEnumerable<short?> values)
    {
        var list = values.Where(v => v != null).Select(v => (decimal)v!.Value).ToList();
        if (list.Count == 0) return null;
        return list.Average();
    }

    internal static decimal StdDev(IEnumerable<decimal> values)
    {
        var list = values.ToList();
        if (list.Count < 2) return 0m;
        var avg = list.Average();
        var variance = list.Sum(v => (v - avg) * (v - avg)) / (list.Count - 1);
        return (decimal)Math.Sqrt((double)variance);
    }
}
