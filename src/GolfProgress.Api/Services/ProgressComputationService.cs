using GolfProgress.Domain.Entities;
using GolfProgress.Api.ErrorHandling;
using GolfProgress.Domain.Enums;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Linq.Expressions;
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
            ?? throw new NotFoundException($"Round {roundId} not found.");

        var result = await StageForRoundAsync(round, windowN, ct);

        // The only save in the compute path. The baseline, the fitness score and
        // both events are written by one SaveChangesAsync, which EF runs as a single
        // transaction, so a failure leaves none of them written rather than some (#8).
        // A single save also stays compatible with the SQL Server retry strategy,
        // which would otherwise require an explicit transaction to run inside
        // IExecutionStrategy.ExecuteAsync.
        await _db.SaveChangesAsync(ct);

        return result;
    }

    /// <summary>
    /// Stages the round's fitness score, the user's current baseline and their events,
    /// without saving, so a caller can write them in the same save as other changes.
    /// The round may be one the context is still adding: logging a round scores it in
    /// the same save that creates it (#80).
    /// </summary>
    public async Task<(UserBaseline baseline, FitnessScore fitness)> StageForRoundAsync(
        Round round, short windowN, CancellationToken ct)
    {
        var fitness = await StageFitnessScoreAsync(round, windowN, ct);

        // The round exists, so the user has at least one round and a baseline.
        var baseline = (await StageCurrentBaselineAsync(round.UserId, round.RoundId, windowN, ct))!;

        return (baseline, fitness);
    }

    /// <summary>
    /// Stages what changes when <paramref name="changed"/> is edited, or removed when
    /// it's null (#81), without saving: the changed round's score, the scores of the
    /// other rounds whose inputs it was or now is part of (<see cref="RoundsAffectedBy"/>),
    /// and the current baseline. The change itself must already be staged on the
    /// context, so the scoring reads the rounds as they will be.
    /// </summary>
    /// <param name="roundId">The edited or deleted round.</param>
    /// <param name="dates">The dates the round was and is played on.</param>
    /// <returns>The current baseline, or null when the user has no rounds left.</returns>
    public async Task<UserBaseline?> StageAfterRoundChangeAsync(
        Guid userId, Guid roundId, Round? changed, IReadOnlyCollection<DateOnly> dates, short windowN, CancellationToken ct)
    {
        var from = dates.Min();
        var later = await _db.Rounds.AsNoTracking()
            .Where(r => r.UserId == userId && r.PlayedAt >= from && r.RoundId != roundId)
            .ToListAsync(ct);

        if (changed is not null)
            await StageFitnessScoreAsync(changed, windowN, ct);

        foreach (var round in RoundsAffectedBy(later, dates, windowN))
            await StageFitnessScoreAsync(round, windowN, ct);

        return await StageCurrentBaselineAsync(userId, roundId, windowN, ct);
    }

    /// <summary>
    /// The rounds, from <paramref name="later"/>, whose score can change when a round
    /// played on one of <paramref name="dates"/> is added, edited or removed (#81).
    ///
    /// A round's score reads two windows of other rounds:
    /// - the scoring baseline: the <paramref name="windowN"/> rounds played before it,
    ///   same-day rounds included. Once <paramref name="windowN"/> rounds have been
    ///   played after day d, every later round's window is full of rounds newer than
    ///   d, so a round on d is in none of them;
    /// - Habits: the rounds in the <see cref="HabitWindowDays"/> days up to it, so
    ///   rounds from d to d + <see cref="HabitWindowDays"/> - 1.
    ///
    /// Rounds outside both keep their score, so an edit to an old round rescores a
    /// handful of rounds rather than everything since. Micro-wins are a snapshot of
    /// the moment they were generated and aren't regenerated for these rounds.
    /// </summary>
    internal static List<Round> RoundsAffectedBy(
        IEnumerable<Round> later, IEnumerable<DateOnly> dates, short windowN)
    {
        var rounds = later.OrderBy(r => r.PlayedAt).ToList();
        var affected = new HashSet<Guid>();

        foreach (var day in dates.Distinct())
        {
            var habitWindowEnd = day.AddDays(HabitWindowDays - 1);
            foreach (var r in rounds.Where(r => r.PlayedAt >= day && r.PlayedAt <= habitWindowEnd))
                affected.Add(r.RoundId);

            var after = rounds.Where(r => r.PlayedAt > day).ToList();
            if (after.Count == 0) continue;

            // The first windowN rounds after the day, and any played on the same day as
            // the last of them: they tie, so either may be the one in the window.
            var lastDay = after[Math.Min(windowN, after.Count) - 1].PlayedAt;
            foreach (var r in after.Where(r => r.PlayedAt <= lastDay))
                affected.Add(r.RoundId);
        }

        return rounds.Where(r => affected.Contains(r.RoundId)).ToList();
    }

    /// <summary>Stages the round's fitness score and its event. Nothing is saved here.</summary>
    private async Task<FitnessScore> StageFitnessScoreAsync(Round round, short windowN, CancellationToken ct)
    {
        // Two different baselines, for two different jobs.
        //
        // The SCORING baseline is the window of rounds played before this one --
        // the yardstick this round is judged against. It is never persisted,
        // because it belongs to this round rather than to the user.
        var priorRounds = await RecentRoundsAsync(round.UserId, windowN, before: round, ct);
        var scoringBaseline = BuildBaseline(round.UserId, windowN, priorRounds);

        var fitness = await UpsertFitnessScoreAsync(
            round, scoringBaseline, scoringBaseline.ConfidenceLevel, ct);

        // Emit events (append-only)
        _db.Events.Add(new EventLog
        {
            EventLogId = Guid.NewGuid(),
            UserId = round.UserId,
            EventType = "FitnessScoreComputed",
            RelatedId = round.RoundId,
            PayloadJson = JsonDocument.Parse("{\"scoreTotal\":" + fitness.ScoreTotal + "}", new JsonDocumentOptions()),
            CreatedAt = DateTimeOffset.UtcNow
        });

        return fitness;
    }

    /// <summary>
    /// Stages the user's current baseline and its event. Nothing is saved here. When
    /// the user has no rounds left, the stored baseline is removed instead, as for a
    /// user who never logged one, and null is returned.
    /// </summary>
    /// <param name="relatedId">The round whose change prompted it, for the event.</param>
    private async Task<UserBaseline?> StageCurrentBaselineAsync(
        Guid userId, Guid relatedId, short windowN, CancellationToken ct)
    {
        // The CURRENT baseline is the user's most recent rounds, and is what
        // UserBaselines stores and GET /me/baseline serves. It is recomputed from
        // scratch on every call and does not depend on which round was just scored,
        // so recomputing an old round can no longer overwrite it with a stale
        // window. Any compute leaves it correct.
        var currentRounds = await RecentRoundsAsync(userId, windowN, before: null, ct);

        if (currentRounds.Count == 0)
        {
            var stored = await _db.UserBaselines.FirstOrDefaultAsync(b => b.UserId == userId, ct);
            if (stored is not null) _db.UserBaselines.Remove(stored);
            return null;
        }

        var baseline = await UpsertCurrentBaselineAsync(
            BuildBaseline(userId, windowN, currentRounds), ct);

        _db.Events.Add(new EventLog
        {
            EventLogId = Guid.NewGuid(),
            UserId = userId,
            EventType = "BaselineComputed",
            RelatedId = relatedId,
            PayloadJson = JsonDocument.Parse("{\"windowRounds\":" + windowN + "}", new JsonDocumentOptions()),
            CreatedAt = DateTimeOffset.UtcNow
        });

        return baseline;
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
    private async Task<List<Round>> RecentRoundsAsync(
        Guid userId, short windowN, Round? before, CancellationToken ct)
    {
        var query = _db.Rounds.AsNoTracking().Where(r => r.UserId == userId);

        // Rounds being logged, edited or deleted in this unit of work: the database
        // still holds them as they were, so their staged state replaces it.
        var changes = UnsavedRoundChanges(userId);
        var changedIds = changes.Select(e => e.Entity.RoundId).ToHashSet();
        var staged = changes.Where(e => e.State != EntityState.Deleted).Select(e => e.Entity);

        if (before is not null)
        {
            Expression<Func<Round, bool>> playedBefore = r =>
                r.PlayedAt < before.PlayedAt ||
                (r.PlayedAt == before.PlayedAt && r.RoundId != before.RoundId);

            query = query.Where(playedBefore);
            staged = staged.Where(playedBefore.Compile());
        }

        var saved = await query
            .OrderByDescending(r => r.PlayedAt)
            // PlayedAt is a date, so rounds on the same day tie. Without a
            // tiebreaker the window was whatever order SQL Server happened to
            // return, which could differ between runs on identical data (#10).
            .ThenByDescending(r => r.CreatedAt)
            // Enough to still fill the window once the stale copies are dropped.
            .Take(windowN + changedIds.Count)
            .ToListAsync(ct);

        return saved.Where(r => !changedIds.Contains(r.RoundId))
            .Concat(staged)
            .OrderByDescending(r => r.PlayedAt)
            .ThenByDescending(r => r.CreatedAt)
            .Take(windowN)
            .ToList();
    }

    /// <summary>
    /// The user's rounds that the context is adding, changing or deleting but hasn't
    /// saved. Queries against the database see them as they were: a round being
    /// logged (#80) isn't there at all, and one being edited or deleted (#81) still is.
    /// </summary>
    private List<EntityEntry<Round>> UnsavedRoundChanges(Guid userId) =>
        _db.ChangeTracker.Entries<Round>()
            .Where(e => (e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                        && e.Entity.UserId == userId)
            .ToList();

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
            // Every figure is averaged as an 18-hole equivalent so a mix of 9- and
            // 18-hole rounds yields one coherent yardstick (#5, #35).
            AvgScore = rounds.Count == 0 ? null : rounds.Average(StrokesPer18),
            ScoreStdDev = rounds.Count < 2 ? null : StdDev(rounds.Select(StrokesPer18)),
            AvgPutts = AvgNullable(rounds.Select(r => HoleScaling.Per18(r.TotalPutts, r))),
            AvgPenalties = AvgNullable(rounds.Select(r => HoleScaling.Per18(r.Penalties, r))),
            AvgThreePutts = AvgNullable(rounds.Select(r => HoleScaling.Per18(r.ThreePutts, r))),
            AvgBlowUpHoles = AvgNullable(rounds.Select(r => HoleScaling.Per18(r.BlowUpHoles, r))),
            AvgFairwaysHit = AvgNullable(rounds.Select(r => HoleScaling.Per18(r.FairwaysHit, r))),
            AvgGir = AvgNullable(rounds.Select(r => HoleScaling.Per18(r.Gir, r))),
            CompletenessRatio = completeness,
            ConfidenceLevel = ConfidenceFrom(rounds.Count, completeness),
            ComputedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Stages the user's single current baseline row. Nothing is saved here; see
    /// <see cref="StageForRoundAsync"/>.
    /// </summary>
    private async Task<UserBaseline> UpsertCurrentBaselineAsync(
        UserBaseline computed, CancellationToken ct)
    {
        var existing = await _db.UserBaselines
            .FirstOrDefaultAsync(b => b.UserId == computed.UserId, ct);

        if (existing is null)
        {
            _db.UserBaselines.Add(computed);
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

        return existing;
    }

    /// <summary>
    /// Stages the round's fitness score. Nothing is saved here; see
    /// <see cref="StageForRoundAsync"/>.
    /// </summary>
    private async Task<FitnessScore> UpsertFitnessScoreAsync(Round round, UserBaseline baseline, ConfidenceLevel confidence, CancellationToken ct)
    {
        var existing = await _db.FitnessScores.FirstOrDefaultAsync(fs => fs.RoundId == round.RoundId, ct);

        var habitSignals = await HabitSignalsForAsync(round, ct);
        var (execution, damage, consistency, habits) = ComputePillars(round, baseline, habitSignals);

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
                habits = new
                {
                    roundsInWindow = habitSignals.RoundsInWindow,
                    practiceSessionsInWindow = habitSignals.PracticeSessionsInWindow,
                    reflected = habitSignals.Reflected
                },
                pillars = new { execution, damage, consistency, habits }
            }),
            new JsonDocumentOptions()
        );

        return existing;
    }

    internal static (short execution, short damage, short consistency, short habits) ComputePillars(
        Round r, UserBaseline b, HabitSignals habitSignals = default)
    {
        // Scoring, 0..PillarMax per pillar. Each pillar can reach PillarMax, so a
        // ScoreTotal of 100 is attainable (#28).
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
        //
        // Deductions are per 18 holes: two penalties in nine holes is the same rate
        // of damage as four in eighteen, and is scored that way (#35).
        var deductions =
            (HoleScaling.Per18((decimal)(r.Penalties ?? 0), r.Holes) * 4) +
            (HoleScaling.Per18((decimal)(r.BlowUpHoles ?? 0), r.Holes) * 2);

        short damage = (short)Math.Clamp(
            DamageCeiling(r) - (int)Math.Round(deductions),
            0, PillarMax);

        // Consistency: avoiding rounds that fall apart, measured against your own
        // spread. Full marks at or better than your average, tapering linearly to 0
        // at ConsistencyZeroAtStdDevs standard deviations worse.
        //
        // It used to give 18 within one std dev either side and 8 outside, so it
        // could never reach 25 and penalised an unusually GOOD round as
        // "inconsistent" -- pulling against Execution, which rewards exactly that
        // round. Together those capped ScoreTotal at 78 (#28). One-sided, the two
        // pillars agree, and an excellent round can score full marks on both.
        short consistency = PillarNeutral;
        if (b.ScoreStdDev != null && b.AvgScore != null && b.ScoreStdDev.Value > 0)
        {
            var stdDevsWorse = Math.Max(0m, strokes - b.AvgScore.Value) / b.ScoreStdDev.Value;
            var fraction = 1m - (stdDevsWorse / ConsistencyZeroAtStdDevs);

            consistency = (short)Math.Clamp(
                (int)Math.Round(PillarMax * fraction, MidpointRounding.AwayFromZero), 0, PillarMax);
        }

        short habits = HabitsScore(habitSignals);

        return (execution, damage, consistency, habits);
    }

    /// <summary>How many std devs worse than average a round must be to score 0 Consistency.</summary>
    internal const decimal ConsistencyZeroAtStdDevs = 2m;

    /// <summary>
    /// Rounds plus practice sessions in the habit window that earn the full
    /// frequency points: about one a week.
    /// </summary>
    internal const int HabitRoundsForFullMarks = 4;

    internal const int HabitWindowDays = 28;
    internal const short HabitFrequencyMax = 15;
    internal const short HabitReflectionPoints = 10;

    /// <summary>
    /// Habits: the behaviours that drive improvement, rather than the result (#28).
    /// Up to <see cref="HabitFrequencyMax"/> for playing or practising regularly,
    /// scaled by rounds and practice sessions in the <see cref="HabitWindowDays"/>
    /// days up to the round (#19), and <see cref="HabitReflectionPoints"/> for
    /// reflecting on this round.
    ///
    /// Practice shares the frequency cap with rounds rather than adding points of
    /// its own, so the pillar still tops out at 25 and a user who does not log
    /// practice scores exactly as before.
    /// </summary>
    internal static short HabitsScore(HabitSignals signals)
    {
        var activities = Math.Clamp(
            signals.RoundsInWindow + signals.PracticeSessionsInWindow, 0, HabitRoundsForFullMarks);
        var frequency = activities * HabitFrequencyMax / HabitRoundsForFullMarks;

        return (short)(frequency + (signals.Reflected ? HabitReflectionPoints : 0));
    }

    /// <summary>
    /// Reads the habit inputs for a round. Measured up to the round's own date rather
    /// than today, so recomputing an old round gives the same answer.
    /// </summary>
    private async Task<HabitSignals> HabitSignalsForAsync(Round round, CancellationToken ct)
    {
        var windowStart = round.PlayedAt.AddDays(-(HabitWindowDays - 1));

        var roundsInWindow = await _db.Rounds.CountAsync(r =>
            r.UserId == round.UserId &&
            r.PlayedAt >= windowStart &&
            r.PlayedAt <= round.PlayedAt, ct);

        // Swap the database's copies of unsaved changes for their staged state: count
        // edited and deleted rounds where they were out, and added and edited ones
        // where they are now.
        bool InWindow(DateOnly playedAt) => playedAt >= windowStart && playedAt <= round.PlayedAt;
        foreach (var change in UnsavedRoundChanges(round.UserId))
        {
            if (change.State != EntityState.Added &&
                InWindow(change.Property(r => r.PlayedAt).OriginalValue)) roundsInWindow--;

            if (change.State != EntityState.Deleted && InWindow(change.Entity.PlayedAt)) roundsInWindow++;
        }

        var practiceInWindow = await _db.PracticeSessions.CountAsync(s =>
            s.UserId == round.UserId &&
            s.PracticedAt >= windowStart &&
            s.PracticedAt <= round.PlayedAt, ct);

        var reflected = await _db.Reflections.AnyAsync(r => r.RoundId == round.RoundId, ct);

        return new HabitSignals(roundsInWindow, reflected, practiceInWindow);
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
    internal static decimal StrokesPer18(Round r) => HoleScaling.Per18((decimal)r.TotalStrokes, r.Holes);

    internal static decimal? AvgNullable(IEnumerable<short?> values) =>
        AvgNullable(values.Select(v => (decimal?)v));

    internal static decimal? AvgNullable(IEnumerable<decimal?> values)
    {
        var list = values.Where(v => v != null).Select(v => v!.Value).ToList();
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

/// <summary>
/// Inputs to the Habits pillar: rounds and practice sessions in the habit window
/// up to and including the scored round, and whether that round has a reflection.
/// </summary>
internal readonly record struct HabitSignals(
    int RoundsInWindow, bool Reflected, int PracticeSessionsInWindow = 0);
